using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class GetEquipementPercentageOwnPlant : IPlugin
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
                    if (TradeRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                    {
                        var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", TradeRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet(false));

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_specifiedequipmenttable'>
                                            <attribute name='lux_whatistheconditionoftheequipment' />
                                            <attribute name='lux_serialnumber' />
                                            <attribute name='lux_purchaseprice' />
                                            <attribute name='lux_newreplacementvalue' />
                                            <attribute name='lux_modelyearandtradename' />
                                            <attribute name='lux_modelnumber' />
                                            <attribute name='lux_currentmarketvalue' />
                                            <attribute name='lux_ageofequipment' />
                                            <attribute name='lux_actualcashvalue' />
                                            <attribute name='lux_asset' />
                                            <attribute name='lux_group' />
                                            <attribute name='lux_locationofequipment' />
                                            <attribute name='lux_percentageofequipment' />
                                            <attribute name='lux_specifiedequipmenttableid' />
                                            <order attribute='lux_modelyearandtradename' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        if (context.MessageName == "Delete")
                        {
                            tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_specifiedequipmenttable'>
                                                    <attribute name='lux_whatistheconditionoftheequipment' />
                                                    <attribute name='lux_serialnumber' />
                                                    <attribute name='lux_purchaseprice' />
                                                    <attribute name='lux_newreplacementvalue' />
                                                    <attribute name='lux_modelyearandtradename' />
                                                    <attribute name='lux_modelnumber' />
                                                    <attribute name='lux_currentmarketvalue' />
                                                    <attribute name='lux_ageofequipment' />
                                                    <attribute name='lux_actualcashvalue' />
                                                    <attribute name='lux_asset' />
                                                    <attribute name='lux_group' />
                                                    <attribute name='lux_locationofequipment' />
                                                    <attribute name='lux_percentageofequipment' />
                                                    <attribute name='lux_specifiedequipmenttableid' />
                                                    <order attribute='lux_modelyearandtradename' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_specifiedequipmenttableid' operator='ne' uiname='' uitype='lux_specifiedequipmenttable' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                        decimal TotalMarketValue = tradeList.Entities.Sum(x => x.Attributes.Contains("lux_currentmarketvalue") ? x.GetAttributeValue<decimal>("lux_currentmarketvalue") : 0M);

                        foreach (var item in tradeList.Entities)
                        {
                            decimal CMV = item.Attributes.Contains("lux_currentmarketvalue") ? item.GetAttributeValue<decimal>("lux_currentmarketvalue") : 0M;
                            item["lux_percentageofequipment"] = CMV * 100M / TotalMarketValue;
                            organizationService.Update(item);
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