using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Linq;

namespace D365Plugins
{
    public class GetTradesSubscribe : IPlugin
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

                    ArrayList list = new ArrayList();

                    if (entity.LogicalName == "lux_subscribepiquote")
                    {
                        var cpeQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet("lux_tradesector", "lux_registeredaddresscountry"));
                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = false;

                        var tradesector = cpeQuote.Attributes.Contains("lux_tradesector") ? cpeQuote.FormattedValues["lux_tradesector"] : "";
                        list.Add(tradesector);

                        var registeredAddressCountry = cpeQuote.Attributes.Contains("lux_registeredaddresscountry") ? cpeQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";
                        if (registeredAddressCountry.Contains("Australia"))
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Financial planning")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }
                        else if (registeredAddressCountry.Contains("United Kingdom"))
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Insurance Brokers")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }
                        else
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Insurance Brokers")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribepimultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                        if (tradeList.Entities.Count() > 0)
                        {
                            foreach (var item in tradeList.Entities)
                            {
                                list.Add(item.Attributes.Contains("lux_tradesector") ? item.FormattedValues["lux_tradesector"] : "");
                                if (registeredAddressCountry.Contains("Australia"))
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Financial planning")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                                else if (registeredAddressCountry.Contains("United Kingdom"))
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Insurance Brokers")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                                else
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Insurance Brokers")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                            }
                        }

                        cpeQuote["lux_selectedtrade"] = String.Join(",", list.ToArray());
                        organizationService.Update(cpeQuote);
                    }
                    else if (entity.LogicalName == "lux_subscribepimultitrade")
                    {
                        var TradeRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        var cpeQuote = organizationService.Retrieve("lux_subscribepiquote", TradeRow.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet("lux_tradesector", "lux_numberofadditionaltradesifapplicable", "lux_registeredaddresscountry"));
                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = false;

                        var tradesector = cpeQuote.Attributes.Contains("lux_tradesector") ? cpeQuote.FormattedValues["lux_tradesector"] : "";
                        list.Add(tradesector);

                        var registeredAddressCountry = cpeQuote.Attributes.Contains("lux_registeredaddresscountry") ? cpeQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";
                        if (registeredAddressCountry.Contains("Australia"))
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Financial planning")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }
                        else if (registeredAddressCountry.Contains("United Kingdom"))
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Insurance Brokers")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }
                        else
                        {
                            if (!(tradesector.Contains("Accountancy") || tradesector.Contains("Architectural") || tradesector.Contains("Consulting Engineer") || tradesector.Contains("Design & Construction") || tradesector.Contains("Insurance Brokers")))
                            {
                                cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                            }
                        }

                        //var noOfAdditionalTrades = cpeQuote.Attributes.Contains("lux_numberofadditionaltradesifapplicable") ? cpeQuote.GetAttributeValue<decimal>("lux_numberofadditionaltradesifapplicable") : 0;

                        //if (context.MessageName != "Delete")
                        //{
                        //    var alltradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                        //                          <entity name='lux_subscribepimultitrade'>
                        //                            <attribute name='lux_trade' />
                        //                            <attribute name='lux_tradesector' />
                        //                            <filter type='and'>
                        //                              <condition attribute='statecode' operator='eq' value='0' />
                        //                              <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                        //                            </filter>
                        //                          </entity>
                        //                        </fetch>";

                        //    if (noOfAdditionalTrades < organizationService.RetrieveMultiple(new FetchExpression(alltradefetch)).Entities.Count())
                        //    {
                        //        throw new InvalidPluginExecutionException("You can't add more number of trades than declared");
                        //    }
                        //}

                        var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribepimultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (context.MessageName == "Delete")
                        {
                            tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribepimultitrade'>
                                                    <attribute name='lux_trade' />
                                                    <attribute name='lux_tradesector' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_subscribepimultitradeid' operator='ne' uiname='' uitype='lux_subscribepimultitrade' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                        }

                        var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                        if (tradeList.Entities.Count() > 0)
                        {
                            foreach (var item in tradeList.Entities)
                            {
                                list.Add(item.Attributes.Contains("lux_tradesector") ? item.FormattedValues["lux_tradesector"] : "");
                                if (registeredAddressCountry.Contains("Australia"))
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Financial planning")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                                else if (registeredAddressCountry.Contains("United Kingdom"))
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Insurance Brokers")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                                else
                                {
                                    if (!(item.FormattedValues["lux_tradesector"].Contains("Accountancy") || item.FormattedValues["lux_tradesector"].Contains("Architectural") || item.FormattedValues["lux_tradesector"].Contains("Consulting Engineer") || item.FormattedValues["lux_tradesector"].Contains("Design & Construction") || item.FormattedValues["lux_tradesector"].Contains("Insurance Brokers")))
                                    {
                                        cpeQuote["lux_isdocumentmiscellaneoustrade"] = true;
                                    }
                                }
                            }
                        }

                        cpeQuote["lux_selectedtrade"] = String.Join(",", list.ToArray());
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