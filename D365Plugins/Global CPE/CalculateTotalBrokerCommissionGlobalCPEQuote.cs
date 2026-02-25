using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalBrokerCommissionGlobalCPEQuote : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.Depth <= 2)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    Entity entity = (Entity)context.InputParameters["Target"];

                    var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", entity.Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_parentquote"));
                    var TechnicalPremium = cpeQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                    var PolicyPremium = cpeQuote.Attributes.Contains("lux_policypremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

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
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
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
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));
                    if (taxList.Entities.Count() > 0)
                    {
                        foreach (var item in taxList.Entities)
                        {
                            Entity application = organizationService.Retrieve("lux_cpequotetaxtype", item.Id, new ColumnSet(false));
                            application["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(application);
                        }
                    }

                    var TechnicalTaxRate = 0M;
                    var PolicyTaxRate = 0M;

                    var AddonTechnicalTaxRate = 0M;
                    var AddonPolicyTaxRate = 0M;

                    if (taxList.Entities.Count() > 0)
                    {
                        TechnicalTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                        PolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                        AddonTechnicalTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                        AddonPolicyTaxRate = taxList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                        Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                        application["lux_totaltaxamount"] = new Money(TechnicalPremium * TechnicalTaxRate / 100);
                        application["lux_policytotaltaxamount"] = new Money(PolicyPremium * PolicyTaxRate / 100);
                        application["lux_policytotaltax"] = PolicyTaxRate;
                        application["lux_addonpolicytotaltax"] = AddonPolicyTaxRate;
                        organizationService.Update(application);
                    }

                    var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (ptList.Entities.Count() > 0)
                    {
                        var TotalCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                        var TotalFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                        var TotalTechnicalBrokerCommAmt = TechnicalPremium * (TotalCommPercentage) / 100 + TotalFlatFee;
                        var TotalPolicyBrokerCommAmt = PolicyPremium * (TotalCommPercentage) / 100 + TotalFlatFee;

                        Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));

                        if (TechnicalPremium != 0)
                        {
                            application["lux_technicalbrokercommissionpercentage"] = TotalTechnicalBrokerCommAmt * 100 / TechnicalPremium;
                            application["lux_technicalbrokercommissionamount"] = new Money(TotalTechnicalBrokerCommAmt);
                        }

                        if (PolicyPremium != 0)
                        {
                            application["lux_policybrokercommissionpercentage"] = TotalPolicyBrokerCommAmt * 100 / PolicyPremium;
                            application["lux_policybrokercommissionamount"] = new Money(TotalPolicyBrokerCommAmt);
                        }

                        organizationService.Update(application);
                        //throw new InvalidPluginExecutionException(CommissionAmount.ToString());
                    }

                    if (taxList.Entities.Count() > 0 || ptList.Entities.Count() > 0)
                    {
                        if (cpeQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                        {
                            if (cpeQuote.Attributes.Contains("lux_terrorismaddon"))
                            {
                                Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", cpeQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));

                                addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                                var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                                var AddonTotalPolicyTaxAmt = AddonPolicyPremium * AddonPolicyTaxRate / 100;

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
                            else
                            {
                                Entity addonappln = new Entity("lux_additionalpolicypremium");

                                addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                                var AddonTotalPolicyTaxAmt = AddonPolicyPremium * AddonPolicyTaxRate / 100;

                                if (ptList.Entities.Count() > 0)
                                {
                                    var TotalAddonCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                                    var TotalAddonFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                                    var AddonTotalPolicyBrokerCommAmt = AddonPolicyPremium * (TotalAddonCommPercentage) / 100 + TotalAddonFlatFee;

                                    if (AddonPolicyPremium != 0)
                                    {
                                        addonappln["lux_addonbrokercommissionpercentage"] = AddonTotalPolicyBrokerCommAmt * 100 / AddonPolicyPremium;
                                        addonappln["lux_addonbrokercommissionamount"] = new Money(AddonTotalPolicyBrokerCommAmt);
                                    }

                                    addonappln["lux_addonphoenixcommissionpercentage"] = new decimal(10);
                                    addonappln["lux_addonaciesmgucommissionpercentage"] = 2.5M;
                                    addonappln["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);
                                }


                                addonappln["lux_addontotaltaxamount"] = new Money(AddonTotalPolicyTaxAmt);
                                addonappln["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var id = organizationService.Create(addonappln);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_terrorismaddon"] = new EntityReference("lux_additionalpolicypremium", id);
                                organizationService.Update(ptAppln);
                            }
                        }
                        else
                        {
                            if (cpeQuote.Attributes.Contains("lux_terrorismaddon"))
                            {
                                if (cpeQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002 && cpeQuote.Attributes.Contains("lux_parentquote"))
                                {
                                    Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));
                                    if (parentAppln.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == false)
                                    {
                                        organizationService.Delete("lux_terrorismaddon", cpeQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id);
                                    }
                                    else
                                    {
                                        Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", cpeQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));
                                        addonappln["lux_addonpolicypremiumbeforetax"] = new Money(0);
                                        addonappln["lux_addontotaltaxamount"] = new Money(0);
                                        organizationService.Update(addonappln);
                                    }
                                }
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