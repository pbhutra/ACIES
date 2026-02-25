using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ACIES
{
    public class CreateAllQuotesPerilReport : CodeActivity
    {
        [RequiredArgument]
        [Input("Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> Application { get; set; }

        [RequiredArgument]
        [Input("Product")]
        public InArgument<string> Product { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            EntityReference applnref = Application.Get<EntityReference>(executionContext);
            Entity appln = new Entity(applnref.LogicalName, applnref.Id);
            appln = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(true));

            var ProductName = Product.Get<string>(executionContext).ToString();
            var schemaName = "";
            var relatedentityName = "";
            var coverFieldName = "";
            if (ProductName == "Property Owners" || ProductName == "Unoccupied")
            {
                schemaName = "lux_propertyownerspremise";
                relatedentityName = "lux_propertyownersapplication";
                coverFieldName = "lux_covers";
            }
            else if (ProductName == "Retail")
            {
                schemaName = "lux_propertyownersretail";
                relatedentityName = "lux_propertyownersapplications";
                coverFieldName = "lux_materialdamagecoverdetails";
            }
            else if (ProductName == "Commercial Combined")
            {
                schemaName = "lux_commercialcombinedapplication";
                relatedentityName = "lux_propertyownersapplications";
                coverFieldName = "lux_materialdamagecoverdetails";
            }
            else if (ProductName == "Pubs & Restaurants" || ProductName == "Hotels and Guesthouses")
            {
                schemaName = "lux_pubsrestaurantspropertyownersapplicatio";
                relatedentityName = "lux_propertyownersapplications";
                coverFieldName = "lux_materialdamagecoverdetails";
            }
            else if (ProductName == "Contractors Combined")
            {
                schemaName = "lux_contractorscombined";
                relatedentityName = "lux_propertyownersapplications";
                coverFieldName = "lux_materialdamagecoverdetails";
            }

            var premisefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                <entity name='{schemaName}'>
                                    <attribute name='{schemaName}id' />
                                    <attribute name='lux_name' />
                                    <attribute name='createdon' />
                                    <attribute name='lux_floodscore' />
                                    <attribute name='lux_crimescore' />
                                    <attribute name='lux_locationnumber' />
                                    <attribute name='lux_subsidencescore' />
                                    <attribute name='lux_excess' />
                                    <attribute name='lux_subsidenceexcess' />
                                    <attribute name='lux_riskpostcode' />
                                    <attribute name='{coverFieldName}' />
                                    <attribute name='lux_approvedeclinecomment' />
                                    <order attribute='lux_name' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='{relatedentityName}' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                    </filter>
                                </entity>
                        </fetch>";

            var floodendorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_applicationendorsements'>
                                        <attribute name='lux_name' />   
                                        <attribute name='createdon' />
                                        <attribute name='lux_endorsementnumber' />
                                        <attribute name='lux_applicationendorsementsid' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_isdeleted' operator='ne' value='1' />
                                          <condition attribute='lux_name' operator='like' value='%Increased Flood Excess%' />
                                          <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                      </entity>
                                    </fetch>";

            var subsendorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_applicationendorsements'>
                                        <attribute name='lux_name' />   
                                        <attribute name='createdon' />
                                        <attribute name='lux_endorsementnumber' />
                                        <attribute name='lux_applicationendorsementsid' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_isdeleted' operator='ne' value='1' />
                                          <condition attribute='lux_name' operator='like' value='%Increased Subsidence Excess -%' />
                                          <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                      </entity>
                                    </fetch>";

            var referralFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_premisereferral'>
                                        <attribute name='lux_premisereferralid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                          <condition attribute='lux_fieldname' operator='eq' value='Flood Score' />
                                        </filter>
                                        <link-entity name='lux_referral' from='lux_referralid' to='lux_referral' visible='false' link-type='outer' alias='refer'>
                                          <attribute name='lux_approvaldeclinecomment' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

            var premises = service.RetrieveMultiple(new FetchExpression(premisefetch)).Entities;
            if (premises.Count > 0)
            {
                foreach (var item in premises)
                {
                    Entity peril = new Entity("lux_allquotesperilreport");
                    peril["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                    peril["lux_riskpostcode"] = item.Attributes.Contains("lux_riskpostcode") ? item.Attributes["lux_riskpostcode"].ToString() : "";
                    peril["lux_locationnumber"] = item.Attributes.Contains("lux_locationnumber") ? Convert.ToInt32(item.Attributes["lux_locationnumber"].ToString()) : 1;
                    peril["lux_insuredname"] = appln.Attributes.Contains("lux_insuredtitle") ? appln.Attributes["lux_insuredtitle"].ToString() : "";
                    peril["lux_floodscore"] = item.Attributes.Contains("lux_floodscore") ? Convert.ToInt32(item.Attributes["lux_floodscore"].ToString()) : 0;
                    peril["lux_subsidencescore"] = item.Attributes.Contains("lux_subsidencescore") ? Convert.ToInt32(item.Attributes["lux_subsidencescore"].ToString()) : 0;
                    peril["lux_policynumber"] = appln.Attributes.Contains("lux_policynumber") ? appln.Attributes["lux_policynumber"].ToString() : "";
                    peril["lux_floodexcess"] = item.Attributes.Contains("lux_excess") ? new Money(Convert.ToDecimal(item.FormattedValues["lux_excess"].ToString().Replace(",", "").Replace("£", ""))) : new Money(0);
                    peril["lux_subsidenceexcess"] = item.Attributes.Contains("lux_subsidenceexcess") ? new Money(Convert.ToDecimal(item.FormattedValues["lux_subsidenceexcess"].ToString().Replace(",", "").Replace("£", ""))) : new Money(0);
                    service.Create(peril);
                }
            }

            if (premises.Count > 0)
            {
                foreach (var item in premises)
                {
                    Entity peril = new Entity("lux_allpoliciesperilreport");
                    peril["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                    peril["lux_policy"] = new EntityReference("lux_policy", appln.Id);
                    peril["lux_insuredname"] = appln.Attributes.Contains("lux_insuredtitle") ? appln.Attributes["lux_insuredtitle"].ToString() : "";
                    peril["lux_policynumber"] = appln.Attributes.Contains("lux_policynumber") ? appln.Attributes["lux_policynumber"].ToString() : "";
                    if (service.RetrieveMultiple(new FetchExpression(floodendorsementFetch)).Entities.Count > 0)
                    {
                        var name = service.RetrieveMultiple(new FetchExpression(floodendorsementFetch)).Entities.FirstOrDefault().Attributes["lux_name"].ToString();
                        var number = Regex.Match(name, @"\d+").Value;
                        peril["lux_floodexcess"] = new Money(Convert.ToDecimal(number));
                    }
                    if (service.RetrieveMultiple(new FetchExpression(subsendorsementFetch)).Entities.Count > 0)
                    {
                        var name = service.RetrieveMultiple(new FetchExpression(subsendorsementFetch)).Entities.FirstOrDefault().Attributes["lux_name"].ToString();
                        var number = Regex.Match(name, @"\d+").Value;
                        peril["lux_subsidenceexcess"] = new Money(Convert.ToDecimal(number));
                    }
                    service.Create(peril);
                }
            }
        }
    }
}
