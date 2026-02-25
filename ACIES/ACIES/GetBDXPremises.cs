using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACIES
{
    public class GetBDXPremises : CodeActivity
    {
        [RequiredArgument]
        [Input("BDX")]
        [ReferenceTarget("lux_bordereau")]
        public InArgument<EntityReference> BDX { get; set; }

        [RequiredArgument]
        [Input("Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> Application { get; set; }

        [RequiredArgument]
        [Input("Policy")]
        [ReferenceTarget("lux_policy")]
        public InArgument<EntityReference> Policy { get; set; }

        [RequiredArgument]
        [Input("Location ID")]
        public InArgument<string> LocationID { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            EntityReference policyref = Policy.Get<EntityReference>(executionContext);
            Entity policy = service.Retrieve("lux_policy", policyref.Id, new ColumnSet(true));

            EntityReference appref = Application.Get<EntityReference>(executionContext);
            Entity appln = service.Retrieve("lux_propertyownersapplications", appref.Id, new ColumnSet(true));

            EntityReference bdxref = BDX.Get<EntityReference>(executionContext);
            Entity bdx1 = service.Retrieve("lux_bordereau", bdxref.Id, new ColumnSet(true));

            Entity bdx = new Entity("lux_bordereau", bdx1.Id);
            if (appln.Attributes.Contains("lux_applicationtype") && appln.FormattedValues["lux_applicationtype"] == "MTA")
            {
                var Applnfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_propertyownersapplications'>
                                                        <attribute name='lux_name' />
                                                        <attribute name='createdon' />
                                                        <attribute name='lux_postcode' />
                                                        <attribute name='lux_insuredtitle' />
                                                        <attribute name='lux_quotenumber' />
                                                        <attribute name='statuscode' />
                                                        <attribute name='lux_inceptiondate' />
                                                        <attribute name='lux_broker' />
                                                        <attribute name='lux_quotedpremium' />
                                                        <attribute name='lux_policytotalcommission' />
                                                        <attribute name='lux_mtabrokercommissionpercentage' />
                                                        <attribute name='lux_lepolicygrosspremium' />
                                                        <attribute name='lux_legalexpensesmtapremium' />
                                                        <attribute name='lux_quotedpremiumbrokercommissionamount' />
                                                        <attribute name='lux_quotedpremiumaciescommissionamount' />
                                                        <attribute name='lux_mtabrokercommission' />
                                                        <attribute name='lux_mtaaciescommission' />
                                                        <attribute name='lux_mtagrosspremium' />
                                                        <attribute name='lux_totalquotedpremiuminciptandfee' />
                                                        <attribute name='lux_mtatotalpremiumincipt' />
                                                        <attribute name='lux_policyfee' />
                                                        <attribute name='lux_policynetpremium' />
                                                        <attribute name='lux_mtanetpremium' />
                                                        <attribute name='lux_lepolicynetpremium' />
                                                        <attribute name='lux_legalexpensesmtanetpremium' />
                                                        <attribute name='lux_quotedpremiumipt' />
                                                        <attribute name='lux_mtaipt' />
                                                        <attribute name='lux_employersliabilitypolicypremium' />
                                                        <attribute name='lux_employersliabilitymtapremium' />
                                                        <attribute name='lux_propertyownersliabilitypolicypremium' />
                                                        <attribute name='lux_propertyownersliabilitymtapremium' />
                                                        <attribute name='lux_publicproductsliabilitypolicypremium' />
                                                        <attribute name='lux_publicproductsliabilitymtapremium' />
                                                        <attribute name='lux_producttype' />
                                                        <attribute name='lux_applicationtype' />
                                                        <attribute name='lux_propertyownersapplicationsid' />
                                                        <order attribute='lux_inceptiondate' descending='true' />
                                                        <order attribute='lux_quotenumber' descending='true' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='statuscode' operator='eq' value='972970006' />
                                                          <condition attribute='lux_policy' operator='eq' uiname='' uitype='lux_policy' value='{policy.Id}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                var mainRecord = service.RetrieveMultiple(new FetchExpression(Applnfetch)).Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970001);
                var mtaRecord = service.RetrieveMultiple(new FetchExpression(Applnfetch)).Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002);

                bdx["lux_grosspremiumpaidthistime"] = new Money(0);
                bdx["lux_commission"] = Convert.ToDecimal(appln.Attributes["lux_policytotalcommission"].ToString().Replace("%", ""));
                bdx["lux_localsubproducerscommission"] = Convert.ToDecimal(appln.Attributes["lux_mtabrokercommissionpercentage"].ToString().Replace("%", ""));
                bdx["lux_brokeragepercentofgrosspremium"] = Convert.ToDecimal(appln.Attributes["lux_mtabrokercommissionpercentage"].ToString().Replace("%", ""));

                var BrokerCommission = appln.Attributes["lux_mtabrokercommissionpercentage"];

                var LEMainPolicyPremium = mainRecord.Sum(x => x.Attributes.Contains("lux_lepolicygrosspremium") ? x.GetAttributeValue<Money>("lux_lepolicygrosspremium").Value : 0);
                var LEMTAPolicyPremium = mtaRecord.Sum(x => x.Attributes.Contains("lux_legalexpensesmtapremium") ? x.GetAttributeValue<Money>("lux_legalexpensesmtapremium").Value : 0);

                var LEPolicyPremium = LEMainPolicyPremium + LEMTAPolicyPremium;
                var LEBrokerCommAmt = LEPolicyPremium * Convert.ToDecimal(BrokerCommission.ToString().Replace("%", "")) / 100;

                var MainBrokerCommAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_quotedpremiumbrokercommissionamount") ? x.GetAttributeValue<Money>("lux_quotedpremiumbrokercommissionamount").Value : 0);
                var MainACIESCommAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_quotedpremiumaciescommissionamount") ? x.GetAttributeValue<Money>("lux_quotedpremiumaciescommissionamount").Value : 0);

                var MTABrokerCommAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtabrokercommission") ? x.GetAttributeValue<Money>("lux_mtabrokercommission").Value : 0);
                var MTAACIESCommAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtaaciescommission") ? x.GetAttributeValue<Money>("lux_mtaaciescommission").Value : 0);

                var CommAmount = MainBrokerCommAmt + MainACIESCommAmt + MTABrokerCommAmt + MTAACIESCommAmt - LEBrokerCommAmt;

                bdx["lux_commissionamount"] = new Money(CommAmount);
                bdx["lux_localsubproducerscommissionamount"] = new Money(MainBrokerCommAmt + MTABrokerCommAmt - LEBrokerCommAmt);

                var MainGWPAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_quotedpremium") ? x.GetAttributeValue<Money>("lux_quotedpremium").Value : 0);
                var MTAGWPAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtagrosspremium") ? x.GetAttributeValue<Money>("lux_mtagrosspremium").Value : 0);

                bdx["lux_totalgrosswrittenpremium"] = new Money(MainGWPAmt + MTAGWPAmt - LEPolicyPremium);

                var MainGrossAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_totalquotedpremiuminciptandfee") ? x.GetAttributeValue<Money>("lux_totalquotedpremiuminciptandfee").Value : 0);
                var MTAGrossAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtatotalpremiumincipt") ? x.GetAttributeValue<Money>("lux_mtatotalpremiumincipt").Value : 0);

                var PolicyFee = mainRecord.Sum(x => x.Attributes.Contains("lux_policyfee") ? x.GetAttributeValue<Money>("lux_policyfee").Value : 0);

                bdx["lux_grosspremium"] = new Money(MainGrossAmt + MTAGrossAmt - PolicyFee - LEPolicyPremium - LEPolicyPremium * 12 / 100);

                var MainNetAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_policynetpremium") ? x.GetAttributeValue<Money>("lux_policynetpremium").Value : 0);
                var MTANetAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtanetpremium") ? x.GetAttributeValue<Money>("lux_mtanetpremium").Value : 0);

                var LEMainNetAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_lepolicynetpremium") ? x.GetAttributeValue<Money>("lux_lepolicynetpremium").Value : 0);
                var LEMTANetAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_legalexpensesmtanetpremium") ? x.GetAttributeValue<Money>("lux_legalexpensesmtanetpremium").Value : 0);

                bdx["lux_netpremiumtolondoninoriginalcurrency"] = new Money(MainNetAmt + MTANetAmt - LEMainNetAmt - LEMTANetAmt);
                bdx["lux_finalnetpremiumoriginalcurrency"] = new Money(MainNetAmt + MTANetAmt - LEMainNetAmt - LEMTANetAmt);

                bdx["lux_brokerageamountoriginalcurrency"] = new Money(MainBrokerCommAmt + MTABrokerCommAmt - LEBrokerCommAmt);

                bdx["lux_otherfeesordeductionsdescription"] = "Admin Fee";
                bdx["lux_otherfeesordeductionsamount"] = new Money(PolicyFee);

                var MainIPTAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_quotedpremiumipt") ? x.GetAttributeValue<Money>("lux_quotedpremiumipt").Value : 0);
                var MTAIPTAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_mtaipt") ? x.GetAttributeValue<Money>("lux_mtaipt").Value : 0);

                bdx["lux_tax1taxtype"] = "IPT";
                bdx["lux_tax1amountoftaxablepremium"] = new Money(MainGWPAmt + MTAGWPAmt - LEPolicyPremium);
                bdx["lux_tax1amount"] = new Money((MainGWPAmt + MTAGWPAmt - LEPolicyPremium) * 12 / 100);

                var MainELAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_employersliabilitypolicypremium") ? x.GetAttributeValue<Money>("lux_employersliabilitypolicypremium").Value : 0);
                var MTAELAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_employersliabilitymtapremium") ? x.GetAttributeValue<Money>("lux_employersliabilitymtapremium").Value : 0);
                bdx["lux_elpremium"] = new Money(MainELAmt + MTAELAmt);

                var MainPOLAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_propertyownersliabilitypolicypremium") ? x.GetAttributeValue<Money>("lux_propertyownersliabilitypolicypremium").Value : 0);
                var MTAPOLAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_propertyownersliabilitymtapremium") ? x.GetAttributeValue<Money>("lux_propertyownersliabilitymtapremium").Value : 0);
                bdx["lux_propertyownersliabilitypremium"] = new Money(MainPOLAmt + MTAPOLAmt);

                var MainPLAmt = mainRecord.Sum(x => x.Attributes.Contains("lux_publicproductsliabilitypolicypremium") ? x.GetAttributeValue<Money>("lux_publicproductsliabilitypolicypremium").Value : 0);
                var MTAPLAmt = mtaRecord.Sum(x => x.Attributes.Contains("lux_publicproductsliabilitymtapremium") ? x.GetAttributeValue<Money>("lux_publicproductsliabilitymtapremium").Value : 0);
                bdx["lux_plpremium"] = new Money(MainPLAmt + MTAPLAmt);

                service.Update(bdx);
            }
        }
    }
}
