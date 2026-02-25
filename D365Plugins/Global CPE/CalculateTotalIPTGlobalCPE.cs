using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalIPTGlobalCPE : IPlugin
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

                    var PremiumRow = organizationService.Retrieve("lux_cpequotetaxtype", entity.Id, new ColumnSet(true));
                    if (PremiumRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_parentquote"));
                        var TechnicalPremium = cpeQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                        var PolicyPremium = cpeQuote.Attributes.Contains("lux_policypremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                        var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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

                        if (context.MessageName == "Delete")
                        {
                            FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_cpequotetaxtypeid' operator='ne' uiname='' uitype='lux_cpequotetaxtype' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                        if (ptList.Entities.Count() > 0)
                        {
                            foreach (var item in ptList.Entities)
                            {
                                Entity application = organizationService.Retrieve("lux_cpequotetaxtype", item.Id, new ColumnSet(false));
                                application["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                organizationService.Update(application);
                            }
                        }

                        if (ptList.Entities.Count() > 0)
                        {
                            var TechnicalTaxRate = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                            var PolicyTaxRate = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970003).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                            var AddonTechnicalTaxRate = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                            var AddonPolicyTaxRate = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_product").Value != 972970002).Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                            Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                            application["lux_totaltaxamount"] = new Money(TechnicalPremium * TechnicalTaxRate / 100);
                            application["lux_policytotaltaxamount"] = new Money(PolicyPremium * PolicyTaxRate / 100);
                            application["lux_policytotaltax"] = PolicyTaxRate;
                            application["lux_addonpolicytotaltax"] = AddonPolicyTaxRate;
                            organizationService.Update(application);

                            if (cpeQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                            {
                                if (cpeQuote.Attributes.Contains("lux_terrorismaddon"))
                                {
                                    Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", cpeQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));

                                    addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                                    var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                                    var AddonTotalPolicyTaxAmt = AddonPolicyPremium * AddonPolicyTaxRate / 100;

                                    addonappln["lux_addontotaltaxamount"] = new Money(AddonTotalPolicyTaxAmt);
                                    organizationService.Update(addonappln);
                                }
                                else
                                {
                                    Entity addonappln = new Entity("lux_additionalpolicypremium");

                                    addonappln["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);

                                    var AddonPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                                    var AddonTotalPolicyTaxAmt = AddonPolicyPremium * AddonPolicyTaxRate / 100;

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
                        else
                        {
                            Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                            application["lux_totaltaxamount"] = new Money(0);
                            application["lux_policytotaltaxamount"] = new Money(0);
                            application["lux_policytotaltax"] = 0M;
                            application["lux_addonpolicytotaltax"] = 0M;
                            organizationService.Update(application);

                            if (cpeQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                            {
                                if (cpeQuote.Attributes.Contains("lux_terrorismaddon"))
                                {
                                    Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", cpeQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(false));
                                    addonappln["lux_addontotaltaxamount"] = new Money(0);
                                    organizationService.Update(addonappln);
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