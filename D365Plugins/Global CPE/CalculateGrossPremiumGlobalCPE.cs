using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateGrossPremiumGlobalCPE : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var PremiumRow = organizationService.Retrieve("lux_contractorsplantandequipmentquotepremui", entity.Id, new ColumnSet(true));
                    if (PremiumRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var CPEQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet(true));

                        //var TechnicalBrokerCommission = CPEQuote.Attributes.Contains("lux_technicalbrokercommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_technicalbrokercommissionpercentage") : 16.75M;
                        //var TechnicalACIESCommission = CPEQuote.Attributes.Contains("lux_technicalmgacommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_technicalmgacommissionpercentage") : 10;

                        //var TotalTechnicalCommission = TechnicalBrokerCommission + TechnicalACIESCommission;

                        //var PolicyBrokerCommission = CPEQuote.Attributes.Contains("lux_policybrokercommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_policybrokercommissionpercentage") : 16.75M;
                        //var PolicyACIESCommission = CPEQuote.Attributes.Contains("lux_policymgacommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_policymgacommissionpercentage") : 10;

                        //var TotalPolicyCommission = PolicyBrokerCommission + PolicyACIESCommission;

                        //throw new InvalidPluginExecutionException(recruitmentQuote.Id.ToString());

                        var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_contractorsplantandequipmentquotepremui'>
                                                <attribute name='lux_name' />
                                                <attribute name='lux_section' />
                                                <attribute name='lux_contractorsplantandequipmentquote' />
                                                <attribute name='lux_technicalpremium' />
                                                <attribute name='lux_policypremium' />
                                                <attribute name='transactioncurrencyid' />
                                                <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{CPEQuote.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var recruitmentList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                        if (recruitmentList.Entities.Count() > 0)
                        {
                            var TechnicalPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                            var PolicyPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                            var TerrorismSection = recruitmentList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006);
                            var TerrorismTechnicalPremium = TerrorismSection != null ? TerrorismSection.Attributes.Contains("lux_technicalpremium") ? TerrorismSection.GetAttributeValue<Money>("lux_technicalpremium").Value : 0 : 0;
                            var TerrorismPolicyPremium = TerrorismSection != null ? TerrorismSection.Attributes.Contains("lux_policypremium") ? TerrorismSection.GetAttributeValue<Money>("lux_policypremium").Value : 0 : 0;

                            Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.Id, new ColumnSet(false));

                            application["lux_technicalpremiumbeforetax"] = new Money((TechnicalPremium - TerrorismTechnicalPremium) /** 0.675M / (1 - TotalTechnicalCommission / 100)*/);
                            application["lux_policypremiumbeforetax"] = new Money((PolicyPremium - TerrorismPolicyPremium) /** 0.675M / (1 - TotalPolicyCommission / 100)*/);

                            if (CPEQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002 && CPEQuote.Attributes.Contains("lux_parentquote"))
                            {
                                Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));

                                var PolicyDuration = (CPEQuote.GetAttributeValue<DateTime>("lux_expirydate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                                var LengthtillNow = (CPEQuote.GetAttributeValue<DateTime>("lux_effectivedate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                                var remainingDays = PolicyDuration - LengthtillNow;

                                var mainPolicyPremium = PolicyPremium - TerrorismPolicyPremium;
                                var parentPolicyPremium = parentAppln.Contains("lux_policypremiumbeforetax") ? parentAppln.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                                var MTAPremium = (mainPolicyPremium - parentPolicyPremium) * remainingDays / PolicyDuration;

                                application["lux_mtapolicypremiumbeforetax"] = new Money(MTAPremium);
                            }
                            organizationService.Update(application);

                            if (TerrorismSection != null)
                            {
                                Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", CPEQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(false));
                                addonappln["lux_addonpolicypremiumbeforetax"] = new Money(TerrorismPolicyPremium /** 0.675M / (1 - TotalPolicyCommission / 100)*/);

                                if (CPEQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002 && CPEQuote.Attributes.Contains("lux_parentquote"))
                                {
                                    Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));
                                    Entity parentApplnAddon = organizationService.Retrieve("lux_additionalpolicypremium", parentAppln.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));

                                    var PolicyDuration = (CPEQuote.GetAttributeValue<DateTime>("lux_expirydate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                                    var LengthtillNow = (CPEQuote.GetAttributeValue<DateTime>("lux_effectivedate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                                    var remainingDays = PolicyDuration - LengthtillNow;

                                    var mainPolicyPremium = TerrorismPolicyPremium;
                                    var parentPolicyPremium = parentApplnAddon.Contains("lux_addonpolicypremiumbeforetax") ? parentApplnAddon.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;

                                    var MTAPremium = (mainPolicyPremium - parentPolicyPremium) * remainingDays / PolicyDuration;

                                    addonappln["lux_addonmtapolicypremiumbeforetax"] = new Money(MTAPremium);
                                }
                                organizationService.Update(addonappln);
                            }
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