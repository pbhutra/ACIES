using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateMTAPremiumGlobalCPEQuote : IPlugin
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

                    var CPEQuote = new Entity();
                    //throw new InvalidPluginExecutionException("Error111".ToString());

                    if (entity.LogicalName == "lux_contractorsplantandequipmentquote")
                    {
                        CPEQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", entity.Id, new ColumnSet("lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_inceptiondate", "lux_expirydate", "lux_effectivedate", "lux_parentquote", "lux_policytotaltax", "lux_addonpolicytotaltax"));

                    }
                    else if (entity.LogicalName == "lux_additionalpolicypremium")
                    {
                        var addon = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        CPEQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", addon.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_inceptiondate", "lux_expirydate", "lux_effectivedate", "lux_parentquote", "lux_policytotaltax", "lux_addonpolicytotaltax"));
                    }

                    var PolicyPremium = CPEQuote.Attributes.Contains("lux_policypremiumbeforetax") ? CPEQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;
                    var PolicytaxPerc = CPEQuote.Attributes.Contains("lux_policytotaltax") ? CPEQuote.GetAttributeValue<decimal>("lux_policytotaltax") : 0;

                    if (CPEQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002 && CPEQuote.Attributes.Contains("lux_parentquote"))
                    {
                        var PolicyDuration = (CPEQuote.GetAttributeValue<DateTime>("lux_expirydate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days + 1;
                        var LengthtillNow = (CPEQuote.GetAttributeValue<DateTime>("lux_effectivedate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                        var remainingDays = PolicyDuration - LengthtillNow;

                        Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));
                        var mainPolicyPremium = PolicyPremium;
                        var parentPolicyPremium = parentAppln.Contains("lux_policypremiumbeforetax") ? parentAppln.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;


                        Entity application = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.Id, new ColumnSet(false));

                        var MTAPremium = (mainPolicyPremium - parentPolicyPremium) * remainingDays / PolicyDuration;
                        application["lux_mtapolicypremiumbeforetax"] = new Money(MTAPremium);
                        application["lux_mtatotaltaxamount"] = new Money(MTAPremium * PolicytaxPerc / 100);

                        organizationService.Update(application);
                    }


                    if (CPEQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970002)
                    {
                        if (CPEQuote.Attributes.Contains("lux_terrorismaddon"))
                        {
                            Entity addonappln = organizationService.Retrieve("lux_additionalpolicypremium", CPEQuote.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet("lux_addonpolicypremiumbeforetax"));

                            if (CPEQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == false)
                            {
                                addonappln["lux_addonpolicypremiumbeforetax"] = new Money(0);
                                addonappln["lux_addontotaltaxamount"] = new Money(0);
                            }

                            var mainPolicyPremium = addonappln.Attributes.Contains("lux_addonpolicypremiumbeforetax") ? addonappln.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                            var parentPolicyPremium = 0M;
                            var AddontaxPerc = CPEQuote.Attributes.Contains("lux_addonpolicytotaltax") ? CPEQuote.GetAttributeValue<decimal>("lux_addonpolicytotaltax") : 0;

                            var PolicyDuration = (CPEQuote.GetAttributeValue<DateTime>("lux_expirydate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days + 1;
                            var LengthtillNow = (CPEQuote.GetAttributeValue<DateTime>("lux_effectivedate") - CPEQuote.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                            var remainingDays = PolicyDuration - LengthtillNow;

                            Entity parentAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", CPEQuote.GetAttributeValue<EntityReference>("lux_parentquote").Id, new ColumnSet(true));
                            if (parentAppln.Contains("lux_terrorismaddon"))
                            {
                                Entity parentApplnAddon = organizationService.Retrieve("lux_additionalpolicypremium", parentAppln.GetAttributeValue<EntityReference>("lux_terrorismaddon").Id, new ColumnSet(true));
                                parentPolicyPremium = parentApplnAddon.Contains("lux_addonpolicypremiumbeforetax") ? parentApplnAddon.GetAttributeValue<Money>("lux_addonpolicypremiumbeforetax").Value : 0;
                            }

                            var MTAPremium = (mainPolicyPremium - parentPolicyPremium) * remainingDays / PolicyDuration;
                            addonappln["lux_addonmtapolicypremiumbeforetax"] = new Money(MTAPremium);
                            addonappln["lux_addonmtatotaltaxamount"] = new Money(MTAPremium * AddontaxPerc / 100);

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