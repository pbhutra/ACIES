using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalLinePercentageGlobalCPE : IPlugin
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

                    if (entity.LogicalName == "lux_phoenixcapacitysplittable")
                    {
                        var PremiumRow = organizationService.Retrieve("lux_phoenixcapacitysplittable", entity.Id, new ColumnSet(true));
                        if (PremiumRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                        {
                            var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_parentquote"));
                            var TechnicalPremium = cpeQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                            var PolicyPremium = cpeQuote.Attributes.Contains("lux_policypremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                            var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            if (context.MessageName == "Delete")
                            {
                                FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_phoenixcapacitysplittableid' operator='ne' uiname='' uitype='lux_phoenixcapacitysplittable' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                            }

                            var capacityList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                            if (capacityList.Entities.Count() > 0)
                            {
                                foreach (var item in capacityList.Entities)
                                {
                                    Entity application = organizationService.Retrieve("lux_phoenixcapacitysplittable", item.Id, new ColumnSet(false));
                                    application["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                    organizationService.Update(application);
                                }
                            }

                            var cpeQuote1 = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                            var TotalLinePer = 0M;
                            var TotalLineAmt = 0M;

                            var capacityFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            foreach (var item in organizationService.RetrieveMultiple(new FetchExpression(capacityFetch)).Entities)
                            {
                                if (item.Attributes.Contains("lux_lineamountcalculated"))
                                {
                                    TotalLineAmt += item.Attributes.Contains("lux_lineamountcalculated") ? item.GetAttributeValue<Money>("lux_lineamountcalculated").Value : 0;
                                }
                                if (item.Attributes.Contains("lux_linepercentagecalculated"))
                                {
                                    TotalLinePer += item.Attributes.Contains("lux_linepercentagecalculated") ? item.GetAttributeValue<decimal>("lux_linepercentagecalculated") : 0;
                                }
                            }
                            cpeQuote1["lux_debrisremovalpercentage"] = TotalLinePer;
                            cpeQuote1["lux_totallineamount"] = new Money(TotalLineAmt);

                            organizationService.Update(cpeQuote1);
                        }
                    }
                    else if (entity.LogicalName == "lux_contractorsplantandequipmentquote")
                    {
                        var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", entity.Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_iscoverrequiredforterrorism", "lux_terrorismaddon", "transactioncurrencyid", "lux_applicationtype", "lux_parentquote"));
                        var TechnicalPremium = cpeQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                        var PolicyPremium = cpeQuote.Attributes.Contains("lux_policypremiumbeforetax") ? cpeQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                        var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (context.MessageName == "Delete")
                        {
                            FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_phoenixcapacitysplittableid' operator='ne' uiname='' uitype='lux_phoenixcapacitysplittable' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var capacityList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                        if (capacityList.Entities.Count() > 0)
                        {
                            foreach (var item in capacityList.Entities)
                            {
                                Entity application = organizationService.Retrieve("lux_phoenixcapacitysplittable", item.Id, new ColumnSet(false));
                                application["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                organizationService.Update(application);
                            }
                        }

                        var cpeQuote1 = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                        var TotalLinePer = 0M;
                        var TotalLineAmt = 0M;

                        var capacityFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixcapacitysplittable'>
                                                    <attribute name='lux_linepercentagecalculated' />
                                                    <attribute name='lux_lineamountcalculated' />
                                                    <attribute name='lux_fee' />
                                                    <attribute name='lux_phoenixcapacitysplittableid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        foreach (var item in organizationService.RetrieveMultiple(new FetchExpression(capacityFetch)).Entities)
                        {
                            if (item.Attributes.Contains("lux_lineamountcalculated"))
                            {
                                TotalLineAmt += item.Attributes.Contains("lux_lineamountcalculated") ? item.GetAttributeValue<Money>("lux_lineamountcalculated").Value : 0;
                            }
                            if (item.Attributes.Contains("lux_linepercentagecalculated"))
                            {
                                TotalLinePer += item.Attributes.Contains("lux_linepercentagecalculated") ? item.GetAttributeValue<decimal>("lux_linepercentagecalculated") : 0;
                            }
                        }
                        cpeQuote1["lux_debrisremovalpercentage"] = TotalLinePer;
                        cpeQuote1["lux_totallineamount"] = new Money(TotalLineAmt);

                        organizationService.Update(cpeQuote1);
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