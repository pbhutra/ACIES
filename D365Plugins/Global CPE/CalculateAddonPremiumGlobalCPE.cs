using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateAddonPremiumGlobalCPE : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    Entity entity = (Entity)context.InputParameters["Target"];

                    var PremiumRow = organizationService.Retrieve("lux_additionalpolicypremium", entity.Id, new ColumnSet(true));
                    if (PremiumRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var ptQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid"));
                        //var TechnicalPremium = ptQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                        //var PolicyPremium = ptQuote.Attributes.Contains("lux_policypremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                        var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpebrokeragent'>
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_percentage' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_broker' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_cpebrokeragentid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{ptQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{ptQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));
                        if (taxList.Entities.Count() > 0)
                        {
                            foreach (var item in taxList.Entities)
                            {
                                Entity application = organizationService.Retrieve("lux_cpequotetaxtype", item.Id, new ColumnSet(false));
                                application["lux_currencyrefreshfield"] = new Money(new Random().Next(int.MinValue, int.MaxValue));
                                organizationService.Update(application);
                            }
                        }

                        var TechnicalTaxRate = 0M;
                        var PolicyTaxRate = 0M;

                        if (taxList.Entities.Count() > 0)
                        {
                            TechnicalTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                            PolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                        }

                        //throw new InvalidPluginExecutionException(PolicyTaxRate.ToString());

                        var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));

                        if (taxList.Entities.Count() > 0 || ptList.Entities.Count() > 0)
                        {
                            Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", ptQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));

                            addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", ptQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                            var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                            var AddonTotalPolicyTaxAmt = AddonPolicyPremium * PolicyTaxRate / 100;

                            if (ptList.Entities.Count() > 0)
                            {
                                var TotalAddonCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                                var TotalAddonFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                var AddonTotalPolicyBrokerCommAmt = AddonPolicyPremium * (TotalAddonCommPercentage) / 100 + TotalAddonFlatFee;

                                if (AddonPolicyPremium != 0)
                                {
                                    addonappln["lux_addonbrokercommissionpercentage"] = AddonTotalPolicyBrokerCommAmt * 100 / AddonPolicyPremium;
                                    addonappln["lux_addonbrokercommissionamount"] = new Money(AddonTotalPolicyBrokerCommAmt);

                                    if (!addonappln.Attributes.Contains("lux_addonphoenixcommissionpercentage"))
                                    {
                                        addonappln["lux_addonphoenixcommissionpercentage"] = new decimal(10);
                                    }

                                    if (!addonappln.Attributes.Contains("lux_addonaciesmgucommissionpercentage"))
                                    {
                                        addonappln["lux_addonaciesmgucommissionpercentage"] = 2.5M;
                                    }
                                }
                            }

                            addonappln["lux_addontotaltaxamount"] = new Money(AddonTotalPolicyTaxAmt);
                            organizationService.Update(addonappln);
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