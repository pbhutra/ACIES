using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalBrokerGlobalCPE : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = new Entity();
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    if (context.MessageName != "Delete")
                    {
                        entity = (Entity)context.InputParameters["Target"];
                    }
                    else
                    {
                        EntityReference e = (EntityReference)context.InputParameters["Target"];
                        entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                    }

                    var PremiumRow = organizationService.Retrieve("lux_cpebrokeragent", entity.Id, new ColumnSet(true));
                    if (PremiumRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var ptQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_parentquote"));
                        var TechnicalPremium = ptQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                        var PolicyPremium = ptQuote.Attributes.Contains("lux_policypremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

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

                        if (context.MessageName == "Delete")
                        {
                            FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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
                                                      <condition attribute='lux_cpebrokeragentid' operator='ne' uiname='' uitype='lux_cpebrokeragent' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                        if (ptList.Entities.Count() > 0)
                        {
                            var TotalCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                            var TotalFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                            //var MaridianCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003 && x.FormattedValues["lux_broker"].ToLower().Contains("meridian")).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);

                            var TotalTechnicalBrokerCommAmt = TechnicalPremium * (TotalCommPercentage) / 100 + TotalFlatFee;
                            var TotalPolicyBrokerCommAmt = PolicyPremium * (TotalCommPercentage) / 100 + TotalFlatFee;

                            Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", ptQuote.Id, new ColumnSet(false));

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

                            if (ptQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                            {
                                var TotalAddonCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                                var TotalAddonFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);
                                //var AddonMaridianCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001 && x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002 && x.FormattedValues["lux_broker"].ToLower().Contains("meridian")).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);

                                if (ptQuote.Attributes.Contains("lux_terrorismaddon"))
                                {
                                    Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", ptQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));

                                    addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", ptQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                                    var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
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
                                    organizationService.Update(addonappln);
                                }
                                else
                                {
                                    Entity addonappln = new Entity("lux_additionalpolicypremium");

                                    addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", ptQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                                    var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                                    var AddonTotalPolicyBrokerCommAmt = AddonPolicyPremium * (TotalAddonCommPercentage) / 100 + TotalAddonFlatFee;

                                    if (AddonPolicyPremium != 0)
                                    {
                                        addonappln["lux_addonbrokercommissionpercentage"] = AddonTotalPolicyBrokerCommAmt * 100 / AddonPolicyPremium;
                                        addonappln["lux_addonbrokercommissionamount"] = new Money(AddonTotalPolicyBrokerCommAmt);
                                    }

                                    addonappln["lux_addonphoenixcommissionpercentage"] = new decimal(10);
                                    addonappln["lux_addonaciesmgucommissionpercentage"] = 2.5M;
                                    addonappln["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", ptQuote.Id);

                                    var id = organizationService.Create(addonappln);

                                    Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", ptQuote.Id, new ColumnSet(false));
                                    ptAppln["lux_terrorismaddon"] = new EntityReference("lux_additionalpolicypremium", id);
                                    organizationService.Update(ptAppln);
                                }
                            }
                            else
                            {
                                if (ptQuote.Attributes.Contains("lux_terrorismaddon"))
                                {
                                    if (ptQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002 && ptQuote.Attributes.Contains("lux_parentquote"))
                                    {
                                        Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", ptQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet("lux_iscoverrequiredforterrorism"));
                                        if (parentAppln.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == false)
                                        {
                                            organizationService.Delete("lux_terrorismaddon", ptQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id);
                                        }
                                        else
                                        {
                                            //throw new InvalidPluginExecutionException("Error111".ToString());
                                            Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", ptQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));
                                            addonappln["lux_addonpolicypremiumbeforetax"] = new Money(0);
                                            addonappln["lux_addontotaltaxamount"] = new Money(0);
                                            organizationService.Update(addonappln);
                                        }
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