using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class CARSingleProjectItems : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = new Entity();
                if (context.MessageName != "Delete")
                {
                    entity = (Entity)context.InputParameters["Target"];
                }
                else
                {
                    EntityReference e = (EntityReference)context.InputParameters["Target"];
                    entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                }

                try
                {
                    var constructionRate = organizationService.Retrieve("lux_constructiontechnicalrate", entity.Id, new ColumnSet(true));
                    var constructionQuote = organizationService.Retrieve("lux_constructionquotes", constructionRate.GetAttributeValue<EntityReference>("lux_constructionquote").Id, new ColumnSet(true));
                    var InceptionDate = constructionQuote.Attributes.Contains("lux_inceptiondate") ? constructionQuote.GetAttributeValue<DateTime>("lux_inceptiondate") : DateTime.UtcNow;
                    var EstimatedContractValue = constructionQuote.GetAttributeValue<Money>("lux_estimatedcontractvalue").Value;

                    var ConstructionListFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_constructiontechnicalrate'>
                                                            <attribute name='lux_suminsuredorigcurr' />
                                                            <attribute name='lux_rate' />
                                                            <attribute name='lux_projectdescriptioncomments' />
                                                            <attribute name='lux_percentage' />
                                                            <attribute name='lux_item' />
                                                            <attribute name='lux_compositerate' />
                                                            <attribute name='lux_ratingmultiplier' />
                                                            <attribute name='lux_periodinmonths' />
                                                            <attribute name='lux_suminsuredorigcurr_base' />
                                                            <attribute name='lux_constructiontechnicalrateid' />
                                                            <order attribute='lux_suminsuredorigcurr' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_constructionquote' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    if (context.MessageName == "Delete")
                    {
                        ConstructionListFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_constructiontechnicalrate'>
                                                            <attribute name='lux_suminsuredorigcurr' />
                                                            <attribute name='lux_rate' />
                                                            <attribute name='lux_projectdescriptioncomments' />
                                                            <attribute name='lux_percentage' />
                                                            <attribute name='lux_item' />
                                                            <attribute name='lux_compositerate' />
                                                            <attribute name='lux_ratingmultiplier' />
                                                            <attribute name='lux_periodinmonths' />
                                                            <attribute name='lux_suminsuredorigcurr_base' />
                                                            <attribute name='lux_constructiontechnicalrateid' />
                                                            <order attribute='lux_suminsuredorigcurr' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_constructionquote' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                                              <condition attribute='lux_constructiontechnicalrateid' operator='ne' uiname='' uitype='lux_constructiontechnicalrate' value='{entity.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                    }

                    var ConstructionList = organizationService.RetrieveMultiple(new FetchExpression(ConstructionListFetch));
                    if (ConstructionList.Entities.Count() > 0)
                    {
                        var BalanceOfWork = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_item").Value == 972970014);
                        var BalanceofWorkExcl = ConstructionList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_item").Value != 972970014);
                        var Balance = EstimatedContractValue - BalanceofWorkExcl.Sum(x => x.Attributes.Contains("lux_suminsuredorigcurr") ? x.GetAttributeValue<Money>("lux_suminsuredorigcurr").Value : 0);

                        if (BalanceOfWork == null)
                        {
                            Entity ent = new Entity("lux_constructiontechnicalrate");
                            ent.Attributes["lux_constructionquote"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                            ent.Attributes["transactioncurrencyid"] = new EntityReference("transactioncurrency", constructionQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            ent.Attributes["lux_item"] = new OptionSetValue(972970014);
                            ent.Attributes["lux_suminsuredorigcurr"] = new Money(Balance);
                            ent.Attributes["lux_percentage"] = Convert.ToDecimal(Balance * 100 / EstimatedContractValue);
                            ent.Attributes["overriddencreatedon"] = Convert.ToDateTime(constructionQuote.FormattedValues["createdon"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

                            var BaseRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_constructionbaserate'>
                                                        <attribute name='lux_ratingfield' />
                                                        <attribute name='lux_from' />
                                                        <attribute name='lux_to' />
                                                        <attribute name='lux_rate' />
                                                        <attribute name='lux_refer' />
                                                        <attribute name='lux_effectivefromdate' />
                                                        <attribute name='lux_validuntildate' />
                                                        <attribute name='lux_constructionbaserateid' />
                                                        <order attribute='lux_ratingfield' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_ratingfield' operator='eq' value='972970001' />
                                                          <condition attribute='lux_to' operator='eq' value='Balance of Works' />
                                                          <condition attribute='lux_effectivefromdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                          <filter type='or'>
                                                            <condition attribute='lux_validuntildate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                            <condition attribute='lux_validuntildate' operator='null' />
                                                          </filter>
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                            var BaseRateList = organizationService.RetrieveMultiple(new FetchExpression(BaseRateFetch));
                            if (BaseRateList.Entities.Count > 0)
                            {
                                var BaseRate = BaseRateList.Entities.FirstOrDefault().GetAttributeValue<decimal>("lux_rate");
                                ent.Attributes["lux_rate"] = BaseRate;
                            }

                            organizationService.Create(ent);

                            if (Balance < 0)
                            {
                                throw new InvalidPluginExecutionException("BALANCE OF WORKS NEGATIVE! ADJUST SPLIT OF WORKS");
                            }
                        }
                        else
                        {
                            Entity ent = new Entity("lux_constructiontechnicalrate", BalanceOfWork.Id);
                            ent.Attributes["lux_constructionquote"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                            ent.Attributes["transactioncurrencyid"] = new EntityReference("transactioncurrency", constructionQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            ent.Attributes["lux_item"] = new OptionSetValue(972970014);
                            ent.Attributes["lux_suminsuredorigcurr"] = new Money(Balance);
                            ent.Attributes["lux_percentage"] = Convert.ToDecimal(Balance * 100 / EstimatedContractValue);

                            var BaseRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_constructionbaserate'>
                                                        <attribute name='lux_ratingfield' />
                                                        <attribute name='lux_from' />
                                                        <attribute name='lux_to' />
                                                        <attribute name='lux_rate' />
                                                        <attribute name='lux_refer' />
                                                        <attribute name='lux_effectivefromdate' />
                                                        <attribute name='lux_validuntildate' />
                                                        <attribute name='lux_constructionbaserateid' />
                                                        <order attribute='lux_ratingfield' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_ratingfield' operator='eq' value='972970001' />
                                                          <condition attribute='lux_to' operator='eq' value='Balance of Works' />
                                                          <condition attribute='lux_effectivefromdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                          <filter type='or'>
                                                             <condition attribute='lux_validuntildate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                             <condition attribute='lux_validuntildate' operator='null' />
                                                          </filter>
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                            var BaseRateList = organizationService.RetrieveMultiple(new FetchExpression(BaseRateFetch));
                            if (BaseRateList.Entities.Count > 0)
                            {
                                var BaseRate = BaseRateList.Entities.FirstOrDefault().GetAttributeValue<decimal>("lux_rate");
                                ent.Attributes["lux_rate"] = BaseRate;
                            }

                            organizationService.Update(ent);

                            if (Balance < 0)
                            {
                                throw new InvalidPluginExecutionException("BALANCE OF WORKS NEGATIVE! ADJUST SPLIT OF WORKS");
                            }
                        }

                        foreach (var item in BalanceofWorkExcl)
                        {
                            Entity ent = new Entity("lux_constructiontechnicalrate", item.Id);
                            ent.Attributes["lux_percentage"] = Convert.ToDecimal((item.Attributes.Contains("lux_suminsuredorigcurr") ? item.GetAttributeValue<Money>("lux_suminsuredorigcurr").Value : 0) * 100 / EstimatedContractValue);

                            var BaseRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_constructionbaserate'>
                                                        <attribute name='lux_ratingfield' />
                                                        <attribute name='lux_from' />
                                                        <attribute name='lux_to' />
                                                        <attribute name='lux_rate' />
                                                        <attribute name='lux_refer' />
                                                        <attribute name='lux_effectivefromdate' />
                                                        <attribute name='lux_validuntildate' />
                                                        <attribute name='lux_constructionbaserateid' />
                                                        <order attribute='lux_ratingfield' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_ratingfield' operator='eq' value='972970001' />
                                                          <condition attribute='lux_to' operator='eq' value='{item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;")}' />
                                                          <condition attribute='lux_effectivefromdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                          <filter type='or'>
                                                             <condition attribute='lux_validuntildate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", InceptionDate)}' />
                                                             <condition attribute='lux_validuntildate' operator='null' />
                                                          </filter>
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                            var BaseRateList = organizationService.RetrieveMultiple(new FetchExpression(BaseRateFetch));
                            if (BaseRateList.Entities.Count > 0)
                            {
                                var BaseRate = BaseRateList.Entities.FirstOrDefault().GetAttributeValue<decimal>("lux_rate");
                                ent.Attributes["lux_rate"] = BaseRate;

                                if (BaseRateList.Entities.FirstOrDefault().FormattedValues["lux_refer"] == "Refer")
                                {
                                    var SingleRateReferral = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_constructionreferral'>
                                                                    <attribute name='lux_suppliedvalue' />
                                                                    <attribute name='lux_fieldname' />
                                                                    <attribute name='lux_declined' />
                                                                    <attribute name='lux_approve' />
                                                                    <attribute name='lux_approvaldate' />
                                                                    <attribute name='lux_additionalinfo' />
                                                                    <attribute name='lux_userapproval' />
                                                                    <attribute name='lux_constructionreferralid' />
                                                                    <order attribute='lux_approvaldate' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_fieldschemaname' operator='eq' value='{item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower()}' />
                                                                      <condition attribute='lux_constructionquotes' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";

                                    var SingleRateList = organizationService.RetrieveMultiple(new FetchExpression(SingleRateReferral));
                                    if (SingleRateList.Entities.Count == 0)
                                    {
                                        Entity referral = new Entity("lux_constructionreferral");
                                        referral["lux_fieldname"] = "Single Project Base Rate Referral";
                                        referral["lux_suppliedvalue"] = item.FormattedValues["lux_item"].ToString();
                                        referral["lux_constructionquotes"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                                        referral["lux_fieldschemaname"] = item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower();
                                        organizationService.Create(referral);
                                    }
                                    else
                                    {
                                        Entity referral = new Entity("lux_constructionreferral", SingleRateList.Entities.FirstOrDefault().Id);
                                        referral["lux_fieldname"] = "Single Project Base Rate Referral";
                                        referral["lux_suppliedvalue"] = item.FormattedValues["lux_item"].ToString();
                                        referral["lux_constructionquotes"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                                        referral["lux_fieldschemaname"] = item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower();
                                        organizationService.Update(referral);
                                    }

                                    Entity application = organizationService.Retrieve("lux_constructionquotes", constructionQuote.Id, new ColumnSet(false));
                                    application["statuscode"] = new OptionSetValue(972970007);
                                    organizationService.Update(application);
                                }
                                else if (BaseRateList.Entities.FirstOrDefault().FormattedValues["lux_refer"] == "Decline")
                                {
                                    var SingleRateReferral = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_constructionreferral'>
                                                                    <attribute name='lux_suppliedvalue' />
                                                                    <attribute name='lux_fieldname' />
                                                                    <attribute name='lux_declined' />
                                                                    <attribute name='lux_approve' />
                                                                    <attribute name='lux_approvaldate' />
                                                                    <attribute name='lux_additionalinfo' />
                                                                    <attribute name='lux_userapproval' />
                                                                    <attribute name='lux_constructionreferralid' />
                                                                    <order attribute='lux_approvaldate' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_fieldschemaname' operator='eq' value='{item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower()}' />
                                                                      <condition attribute='lux_constructionquotes' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";

                                    var SingleRateList = organizationService.RetrieveMultiple(new FetchExpression(SingleRateReferral));
                                    if (SingleRateList.Entities.Count == 0)
                                    {
                                        Entity referral = new Entity("lux_constructionreferral");
                                        referral["lux_fieldname"] = "Single Project Base Rate Referral";
                                        referral["lux_suppliedvalue"] = item.FormattedValues["lux_item"].ToString();
                                        referral["lux_declined"] = true;
                                        referral["lux_constructionquotes"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                                        referral["lux_fieldschemaname"] = item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower();
                                        organizationService.Create(referral);
                                    }
                                    else
                                    {
                                        Entity referral = new Entity("lux_constructionreferral", SingleRateList.Entities.FirstOrDefault().Id);
                                        referral["lux_fieldname"] = "Single Project Base Rate Referral";
                                        referral["lux_suppliedvalue"] = item.FormattedValues["lux_item"].ToString();
                                        referral["lux_declined"] = true;
                                        referral["lux_constructionquotes"] = new EntityReference("lux_constructionquotes", constructionQuote.Id);
                                        referral["lux_fieldschemaname"] = item.FormattedValues["lux_item"].ToString().Replace("&", "&amp;").Replace("'", "&apos;").Replace("\"", " &quot;").ToLower();
                                        organizationService.Update(referral);
                                    }

                                    Entity application = organizationService.Retrieve("lux_constructionquotes", constructionQuote.Id, new ColumnSet(false));
                                    application["statuscode"] = new OptionSetValue(972970001);
                                    organizationService.Update(application);
                                }

                            }

                            ent["transactioncurrencyid"] = new EntityReference("transactioncurrency", constructionQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(ent);
                        }

                        if (context.MessageName != "Delete")
                        {
                            CalculateRollupFieldRequest request = new CalculateRollupFieldRequest
                            {
                                Target = new EntityReference("lux_constructionquotes", constructionQuote.Id),
                                FieldName = "lux_overallcompositerate"// Rollup Field Name
                            };
                            CalculateRollupFieldResponse response = (CalculateRollupFieldResponse)organizationService.Execute(request);
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}