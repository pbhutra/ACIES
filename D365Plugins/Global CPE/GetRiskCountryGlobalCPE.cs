using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Linq;

namespace D365Plugins
{
    public class GetRiskCountryGlobalCPE : IPlugin
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

                    var TradeRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    if (TradeRow.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", TradeRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_registeredaddresscountry"));
                        ArrayList list = new ArrayList();
                        var Country = cpeQuote.Attributes.Contains("lux_registeredaddresscountry") ? cpeQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpelocation'>
                                                    <attribute name='lux_locationcountry' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (context.MessageName == "Delete")
                        {
                            tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpelocation'>
                                                    <attribute name='lux_locationcountry' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_cpelocationid' operator='ne' uiname='' uitype='lux_cpelocation' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                        if (tradeList.Entities.Count() > 0)
                        {
                            foreach (var item in tradeList.Entities)
                            {
                                list.Add(item.Attributes.Contains("lux_locationcountry") ? (list.Contains(item.Attributes["lux_locationcountry"].ToString()) ? "" : item.Attributes["lux_locationcountry"].ToString()) : "");
                            }
                        }
                        cpeQuote["lux_locationcountry"] = String.Join(",", list.ToArray());

                        organizationService.Update(cpeQuote);
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