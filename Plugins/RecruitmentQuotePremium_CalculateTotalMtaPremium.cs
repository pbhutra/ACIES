using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Plugins
{
    public class RecruitmentQuotePremium_CalculateTotalMtaPremium : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("RecruitmentQuotePremium_CalculateTotalMtaPremium execution started.");

            try
            {
                // Validate the target
                if (!(context.InputParameters["Target"] is Entity target) || target.LogicalName != "lux_specialistschemerecruitmentpremuim")
                    return;

                string messageName = context.MessageName.ToLower();

                if ((messageName != "update") || context.Stage != 40)
                    return;

                EntityReference quoteRef = null;

                if (target.Contains("lux_recruitmentquote"))
                {
                    quoteRef = target.GetAttributeValue<EntityReference>("lux_recruitmentquote");
                }
                else if (context.PreEntityImages.Contains("PreImage") && context.PreEntityImages["PreImage"].Contains("lux_recruitmentquote"))
                {
                    quoteRef = context.PreEntityImages["PreImage"].GetAttributeValue<EntityReference>("lux_recruitmentquote");
                }

                if (quoteRef == null)
                {
                    tracingService.Trace("Quote reference not found. Exiting plugin.");
                    return;
                }

                //QueryExpression quoteQuery = new QueryExpression("lux_recruitmentquote")
                //{
                //    ColumnSet = new ColumnSet("lux_mtabrokercommission", "lux_mtabrokercommissionamount", "lux_mtaaciescommission", "lux_mtaaciescommissionamount",
                //   "lux_mtatotalcommission", "lux_mtatotalcommissionamount", "lux_mtapolicypremiumbeforetax", "lux_mtalegalpremiumbeforetax", "lux_mtatax", "lux_mtataxamount",
                //   "lux_mtapolicyfee", "lux_mtatotalpolicypremiuminctaxandfee"),
                //    Criteria =
                //    {
                //        Conditions =
                //        {
                //            new ConditionExpression("lux_recruitmentquotesid", ConditionOperator.Equal, quoteRef.Id)
                //        }
                //    }
                //};

                ColumnSet quoteCols = new ColumnSet("lux_mtabrokercommission", "lux_mtabrokercommissionamount", "lux_mtaaciescommission", "lux_mtaaciescommissionamount",
                   "lux_mtatotalcommission", "lux_mtatotalcommissionamount", "lux_mtapolicypremiumbeforetax", "lux_mtalegalpremiumbeforetax", "lux_mtatax", "lux_mtataxamount",
                   "lux_mtapolicyfee", "lux_mtatotalpolicypremiuminctaxandfee");

                Entity quote = service.Retrieve("lux_recruitmentquotes", quoteRef.Id, quoteCols);

                string mtaPolicyPremiumFieldName = "lux_mtapolicypremium";
                string sectionFieldName = "lux_section";
                // Section value for Legal
                int LEGAL_SECTION_VALUE = 972970023;

                // Retrieve all premiums related to the same quote
                QueryExpression query = new QueryExpression("lux_specialistschemerecruitmentpremuim")
                {
                    ColumnSet = new ColumnSet("lux_section", mtaPolicyPremiumFieldName),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("lux_recruitmentquote", ConditionOperator.Equal, quoteRef.Id)
                            //new ConditionExpression("lux_section",ConditionOperator.NotEqual,972970023)
                        }
                    }
                };

                EntityCollection premiumRecords = service.RetrieveMultiple(query);

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
                Entity quoteToUpdate = new Entity("lux_recruitmentquotes", quoteRef.Id);
                quoteToUpdate["lux_mtapolicypremiumbeforetax"] = new Money(totalPremium);
                quoteToUpdate["lux_mtabrokercommissionamount"] = new Money(brokerCommissionAmnt);
                quoteToUpdate["lux_mtaaciescommissionamount"] = new Money(aciesCommissionAmnt);
                quoteToUpdate["lux_mtatotalcommissionamount"] = new Money(brokerCommissionAmnt + aciesCommissionAmnt);
                quoteToUpdate["lux_mtalegalpremiumbeforetax"] = new Money(legalPremium);
                quoteToUpdate["lux_mtataxamount"] = new Money(taxAmnt);
                quoteToUpdate["lux_mtatotalpolicypremiuminctaxandfee"] = new Money(totalPolicyPremiumInc);

                service.Update(quoteToUpdate);
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Exception: {ex}");
                throw new InvalidPluginExecutionException("Error calculating total premium on quote.", ex);
            }
        }
    }
}
