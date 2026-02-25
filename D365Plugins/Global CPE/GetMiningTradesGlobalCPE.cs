using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class GetMiningTradesGlobalCPE : IPlugin
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

                    var isMiningTrade = false;

                    if (context.MessageName != "Delete")
                    {
                        entity = (Entity)context.InputParameters["Target"];
                    }
                    else
                    {
                        EntityReference e = (EntityReference)context.InputParameters["Target"];
                        entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                    }

                    if (entity.LogicalName == "lux_contractorsplantandequipmentquote")
                    {
                        var cpeQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet("lux_primarytrade", "lux_tradesector", "lux_businessdescription"));
                        var tradename = cpeQuote.Attributes.Contains("lux_primarytrade") ? cpeQuote.FormattedValues["lux_primarytrade"] : "";
                        var tradesector = cpeQuote.Attributes.Contains("lux_tradesector") ? cpeQuote.FormattedValues["lux_tradesector"] : "";
                        var businessDescription = cpeQuote.Attributes.Contains("lux_businessdescription") ? cpeQuote.Attributes["lux_businessdescription"].ToString() : "";

                        if (tradename.ToLower().Contains("mining") || tradesector.ToLower().Contains("mining") || businessDescription.ToLower().Contains("mining"))
                        {
                            isMiningTrade = true;
                        }
                        else
                        {
                            isMiningTrade = false;
                        }

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpemultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                    <link-entity name='lux_cpetrade' from='lux_cpetradeid' to='lux_trade' link-type='inner' alias='a_4ae88365190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                    <link-entity name='lux_cpetradesector' from='lux_cpetradesectorid' to='lux_tradesector' link-type='inner' alias='a_de288c84190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>";

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                        if (tradeList.Entities.Count() > 0)
                        {
                            isMiningTrade = true;
                        }

                        if (isMiningTrade == true)
                        {
                            cpeQuote["lux_isminingtrade"] = true;
                        }
                        else
                        {
                            cpeQuote["lux_isminingtrade"] = false;
                        }

                        organizationService.Update(cpeQuote);
                    }
                    else if (entity.LogicalName == "lux_cpemultitrade")
                    {
                        var TradeRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        if (TradeRow.Attributes.Contains("lux_contractorsplantandequipmentquote"))
                        {
                            var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", TradeRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_primarytrade", "lux_tradesector", "lux_businessdescription", "lux_numberofadditionaltradesifapplicable"));

                            var tradename = cpeQuote.Attributes.Contains("lux_primarytrade") ? cpeQuote.FormattedValues["lux_primarytrade"] : "";
                            var tradesector = cpeQuote.Attributes.Contains("lux_tradesector") ? cpeQuote.FormattedValues["lux_tradesector"] : "";
                            var businessDescription = cpeQuote.Attributes.Contains("lux_businessdescription") ? cpeQuote.Attributes["lux_businessdescription"].ToString() : "";
                            //var noOfAdditionalTrades = cpeQuote.Attributes.Contains("lux_numberofadditionaltradesifapplicable") ? cpeQuote.GetAttributeValue<decimal>("lux_numberofadditionaltradesifapplicable") : 0;
                            //throw new InvalidPluginExecutionException(cpeQuote.Id.ToString());
                            //if (context.MessageName != "Delete")
                            //{
                            //    var alltradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                      <entity name='lux_cpemultitrade'>
                            //                        <attribute name='lux_trade' />
                            //                        <attribute name='lux_tradesector' />
                            //                        <filter type='and'>
                            //                          <condition attribute='statecode' operator='eq' value='0' />
                            //                          <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                            //                        </filter>
                            //                      </entity>
                            //                    </fetch>";

                            //    if (noOfAdditionalTrades < organizationService.RetrieveMultiple(new FetchExpression(alltradefetch)).Entities.Count())
                            //    {
                            //        throw new InvalidPluginExecutionException("You can't add more number of trades than declared");
                            //    }
                            //}

                            var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpemultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                    <link-entity name='lux_cpetrade' from='lux_cpetradeid' to='lux_trade' link-type='inner' alias='a_4ae88365190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                    <link-entity name='lux_cpetradesector' from='lux_cpetradesectorid' to='lux_tradesector' link-type='inner' alias='a_de288c84190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>";

                            if (context.MessageName == "Delete")
                            {
                                tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpemultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_cpemultitradeid' operator='ne' uiname='' uitype='lux_cpemultitrade' value='{entity.Id}' />
                                                    </filter>
                                                    <link-entity name='lux_cpetrade' from='lux_cpetradeid' to='lux_trade' link-type='inner' alias='a_4ae88365190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                    <link-entity name='lux_cpetradesector' from='lux_cpetradesectorid' to='lux_tradesector' link-type='inner' alias='a_de288c84190ef011998a7c1e52033c59'>
                                                      <attribute name='lux_name' />
                                                      <filter type='and'>
                                                        <condition attribute='lux_name' operator='like' value='%mining%' />
                                                      </filter>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>";
                            }

                            var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                            if (tradeList.Entities.Count() > 0)
                            {
                                isMiningTrade = true;
                            }
                            else
                            {
                                isMiningTrade = false;
                            }

                            if (tradename.ToLower().Contains("mining") || tradesector.ToLower().Contains("mining") || businessDescription.ToLower().Contains("mining"))
                            {
                                isMiningTrade = true;
                            }

                            if (isMiningTrade == true)
                            {
                                cpeQuote["lux_isminingtrade"] = true;
                            }
                            else
                            {
                                cpeQuote["lux_isminingtrade"] = false;
                            }
                            organizationService.Update(cpeQuote);
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