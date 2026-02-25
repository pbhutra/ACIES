using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace Acies_Customization.Custom_Workflow
{
    public class CalculateMTAPremiumRecruitment : CodeActivity
    {
        [Input("Recruitment Quote")]
        [ReferenceTarget("lux_recruitmentquotes")]
        public InArgument<EntityReference> RecruitmentQuote { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            Entity recruitmentQuote = service.Retrieve("lux_recruitmentquotes", RecruitmentQuote.Get<EntityReference>(executionContext).Id, new ColumnSet(true));
            Entity parentRecruitmentQuote = service.Retrieve("lux_recruitmentquotes", recruitmentQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));

            var LECover = recruitmentQuote.Attributes.Contains("lux_islegalexpensescoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_islegalexpensescoverrequired") : false;
            var parentLeCover = parentRecruitmentQuote.Attributes.Contains("lux_islegalexpensescoverrequired") ? parentRecruitmentQuote.GetAttributeValue<bool>("lux_islegalexpensescoverrequired") : false;

            var expiryDate = Convert.ToDateTime(recruitmentQuote.FormattedValues["lux_expirydate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);
            var inceptionDate = Convert.ToDateTime(recruitmentQuote.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);
            var mtaDate = Convert.ToDateTime(recruitmentQuote.FormattedValues["lux_effectivedate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

            var PolicyDuration = (expiryDate - inceptionDate).Days + 1;
            var LengthtillNow = (mtaDate - inceptionDate).Days;
            var remainingDays = PolicyDuration - LengthtillNow - 1;

            var ParentRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_specialistschemerecruitmentpremuim'>
                                <attribute name='lux_name' />
                                <attribute name='lux_section' />
                                <attribute name='lux_ratingfigures' />
                                <attribute name='lux_roworder' />
                                <attribute name='lux_loaddiscount' />
                                <attribute name='lux_technicalpremium' />
                                <attribute name='lux_recruitmentquote' />
                                <attribute name='transactioncurrencyid' />
                                <attribute name='lux_specialistschemerecruitmentpremuimid' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{parentRecruitmentQuote.Id}' />
                                </filter>
                              </entity>
                            </fetch>";

            var ParentRateItem = service.RetrieveMultiple(new FetchExpression(ParentRatingfetch)).Entities;

            var Ratingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_specialistschemerecruitmentpremuim'>
                                <attribute name='lux_name' />
                                <attribute name='lux_section' />
                                <attribute name='lux_ratingfigures' />
                                <attribute name='lux_roworder' />
                                <attribute name='lux_loaddiscount' />
                                <attribute name='lux_technicalpremium' />
                                <attribute name='lux_recruitmentquote' />
                                <attribute name='transactioncurrencyid' />
                                <attribute name='lux_specialistschemerecruitmentpremuimid' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                                </filter>
                              </entity>
                            </fetch>";

            var RateItem = service.RetrieveMultiple(new FetchExpression(Ratingfetch)).Entities;

            foreach (var item in ParentRateItem)
            {
                Entity rateItem = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == item.GetAttributeValue<OptionSetValue>("lux_section").Value);
                if (rateItem != null)
                {
                    var ParentTechnicalPremium = item.Contains("lux_technicalpremium") ? item.GetAttributeValue<Money>("lux_technicalpremium").Value : 0;
                    var CurrentTechnicalPremium = rateItem.Contains("lux_technicalpremium") ? rateItem.GetAttributeValue<Money>("lux_technicalpremium").Value : 0;

                    var MTATechnicalPremium = (CurrentTechnicalPremium - ParentTechnicalPremium) * remainingDays / PolicyDuration;
                    var load = item.Contains("lux_loaddiscount") ? item.GetAttributeValue<decimal>("lux_loaddiscount") : 0;

                    rateItem["lux_mtatechnicalpremium"] = new Money(MTATechnicalPremium);
                    rateItem["lux_mtapolicypremium"] = new Money(MTATechnicalPremium + MTATechnicalPremium * load / 100);
                    service.Update(rateItem);
                }
                else
                {
                    var premiumEntity = new Entity("lux_specialistschemerecruitmentpremuim");

                    premiumEntity["lux_section"] = new OptionSetValue(item.GetAttributeValue<OptionSetValue>("lux_section").Value);
                    premiumEntity["lux_name"] = item.Attributes["lux_name"];
                    premiumEntity["lux_ratingfigures"] = new Money(0);
                    premiumEntity["lux_technicalpremium"] = new Money(0);
                    premiumEntity["lux_policypremium_manual"] = new Money(0);
                    premiumEntity["lux_recruitmentquote"] = new EntityReference("lux_recruitmentquotes", recruitmentQuote.Id);
                    premiumEntity["lux_roworder"] = item.GetAttributeValue<int>("lux_roworder");
                    premiumEntity["lux_ismtaonlyrow"] = true;

                    var ParentTechnicalPremium = item.Contains("lux_technicalpremium") ? item.GetAttributeValue<Money>("lux_technicalpremium").Value : 0;
                    var CurrentTechnicalPremium = 0M;

                    var MTATechnicalPremium = (CurrentTechnicalPremium - ParentTechnicalPremium) * remainingDays / PolicyDuration;
                    if (item.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970023)
                    {
                        MTATechnicalPremium = 0;
                    }

                    if (item.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970025)
                    {
                        if ((ParentTechnicalPremium + MTATechnicalPremium) < 150)
                        {
                            MTATechnicalPremium = -1 * (ParentTechnicalPremium - 150);
                        }
                    }

                    premiumEntity["lux_mtatechnicalpremium"] = new Money(MTATechnicalPremium);

                    var load = item.Contains("lux_loaddiscount") ? item.GetAttributeValue<decimal>("lux_loaddiscount") : 0;
                    premiumEntity["lux_mtapolicypremium"] = new Money(MTATechnicalPremium + MTATechnicalPremium * load / 100);

                    service.Create(premiumEntity);
                }
            }

            foreach (var item in RateItem)
            {
                Entity parentRateItem = ParentRateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == item.GetAttributeValue<OptionSetValue>("lux_section").Value);
                if (parentRateItem == null)
                {
                    var ParentTechnicalPremium = 0;
                    var CurrentTechnicalPremium = item.Contains("lux_technicalpremium") ? item.GetAttributeValue<Money>("lux_technicalpremium").Value : 0;

                    var MTATechnicalPremium = (CurrentTechnicalPremium - ParentTechnicalPremium) * remainingDays / PolicyDuration;
                    var load = item.Contains("lux_loaddiscount") ? item.GetAttributeValue<decimal>("lux_loaddiscount") : 0;

                    if (item.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970023)
                    {
                        MTATechnicalPremium = CurrentTechnicalPremium - ParentTechnicalPremium;
                    }

                    if (item.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970025)
                    {
                        if (MTATechnicalPremium < 150)
                        {
                            MTATechnicalPremium = 150;
                        }
                    }

                    item["lux_mtatechnicalpremium"] = new Money(MTATechnicalPremium);
                    item["lux_mtapolicypremium"] = new Money(MTATechnicalPremium + MTATechnicalPremium * load / 100);

                    service.Update(item);
                }
            }


            ColumnSet quoteCols = new ColumnSet("lux_mtabrokercommission", "lux_mtabrokercommissionamount", "lux_mtaaciescommission", "lux_mtaaciescommissionamount",
                   "lux_mtatotalcommission", "lux_mtatotalcommissionamount", "lux_mtapolicypremiumbeforetax", "lux_mtalegalpremiumbeforetax", "lux_mtatax", "lux_mtataxamount",
                   "lux_mtapolicyfee", "lux_mtatotalpolicypremiuminctaxandfee");

            Entity quote = service.Retrieve("lux_recruitmentquotes", recruitmentQuote.Id, quoteCols);

            string mtaPolicyPremiumFieldName = "lux_mtapolicypremium";
            string sectionFieldName = "lux_section";
            // Section value for Legal
            int LEGAL_SECTION_VALUE = 972970023;

            var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_specialistschemerecruitmentpremuim'>
                                                <attribute name='lux_name' />
                                                <attribute name='lux_section' />
                                                <attribute name='lux_recruitmentquote' />
                                                <attribute name='lux_mtapolicypremium' />
                                                <attribute name='transactioncurrencyid' />
                                                <attribute name='lux_specialistschemerecruitmentpremuimid' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                                                  <condition attribute='lux_section' operator='in'>
                                                    <value>972970012</value>
                                                    <value>972970013</value>
                                                    <value>972970023</value>
                                                    <value>972970024</value>
                                                    <value>972970025</value>
                                                    <value>972970026</value>
                                                  </condition>
                                                </filter>
                                              </entity>
                                            </fetch>";

            EntityCollection premiumRecords = service.RetrieveMultiple(new FetchExpression(FinalRatingfetch));

            decimal totalPremium = 0M;
            decimal legalPremium = 0M;

            foreach (var premium in premiumRecords.Entities)
            {
                var sectionValue = premium.GetAttributeValue<OptionSetValue>(sectionFieldName)?.Value;

                if (premium.Contains(mtaPolicyPremiumFieldName) && premium[mtaPolicyPremiumFieldName] != null)
                {
                    decimal premiumValue = premium.GetAttributeValue<Money>(mtaPolicyPremiumFieldName).Value;

                    if (sectionValue == LEGAL_SECTION_VALUE)
                    {
                        legalPremium += premiumValue;
                    }
                    else
                    {
                        totalPremium += premiumValue;
                    }
                }
            }

            decimal totalPremiumIncLegal = totalPremium + legalPremium;

            decimal brokerCommissionPerc = quote.GetAttributeValue<decimal>("lux_mtabrokercommission");
            decimal aciesCommissionPerc = quote.GetAttributeValue<decimal>("lux_mtaaciescommission");
            decimal totalCommissionPerc = quote.GetAttributeValue<decimal>("lux_mtatotalcommission");
            decimal taxPerc = quote.GetAttributeValue<decimal>("lux_mtatax");
            decimal policyFee = quote.GetAttributeValue<Money>("lux_mtapolicyfee").Value;


            decimal brokerCommissionAmnt = totalPremiumIncLegal * (brokerCommissionPerc / 100);
            decimal aciesCommissionAmnt = totalPremiumIncLegal * (aciesCommissionPerc / 100);
            decimal totalCommissionAmnt = totalPremiumIncLegal * (totalCommissionPerc / 100);
            decimal taxAmnt = totalPremiumIncLegal * (taxPerc / 100);

            decimal totalPolicyPremiumInc = totalPremiumIncLegal + taxAmnt + policyFee;

            // Update the Quote
            Entity quoteToUpdate = new Entity("lux_recruitmentquotes", recruitmentQuote.Id);
            quoteToUpdate["lux_mtapolicypremiumbeforetax"] = new Money(totalPremium);
            quoteToUpdate["lux_mtabrokercommissionamount"] = new Money(brokerCommissionAmnt);
            quoteToUpdate["lux_mtaaciescommissionamount"] = new Money(aciesCommissionAmnt);
            quoteToUpdate["lux_mtatotalcommissionamount"] = new Money(brokerCommissionAmnt + aciesCommissionAmnt);
            quoteToUpdate["lux_mtalegalpremiumbeforetax"] = new Money(legalPremium);
            quoteToUpdate["lux_mtataxamount"] = new Money(taxAmnt);
            quoteToUpdate["lux_mtatotalpolicypremiuminctaxandfee"] = new Money(totalPolicyPremiumInc);

            service.Update(quoteToUpdate);
        }
    }
}
