using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Linq;

namespace D365Plugins
{
    public class GetUKCountrySubscribe : IPlugin
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
                    var cpeQuote = organizationService.Retrieve("lux_subscribepiquote", TradeRow.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet("lux_registeredaddresscountry"));
                    ArrayList list = new ArrayList();
                    var Country = cpeQuote.Attributes.Contains("lux_registeredaddresscountry") ? cpeQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";
                    var IsUKCountry = Country.Contains("United Kingdom");
                    //list.Add(Country);

                    var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribepilocation'>
                                                    <attribute name='lux_country' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    if (context.MessageName == "Delete")
                    {
                        tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribepilocation'>
                                                    <attribute name='lux_country' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_subscribepilocationid' operator='ne' uiname='' uitype='lux_subscribepilocation' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    }

                    var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                    //if ((tradeList.Entities.Count() > 0 && tradeList.Entities.FirstOrDefault(x => x.Attributes["lux_country"].ToString().Contains("United Kingdom")) != null) || IsUKCountry == true)
                    //{
                    //    cpeQuote["lux_isemployeetrade"] = true;
                    //}
                    //else
                    //{
                    //    cpeQuote["lux_isemployeetrade"] = false;
                    //}

                    if (tradeList.Entities.Count() > 0)
                    {
                        foreach (var item in tradeList.Entities)
                        {
                            list.Add(item.Attributes.Contains("lux_country") ? (list.Contains(item.Attributes["lux_country"].ToString()) ? "" : item.Attributes["lux_country"].ToString()) : "");
                        }
                    }
                    cpeQuote["lux_risklocationcountry"] = String.Join(",", list.ToArray());

                    organizationService.Update(cpeQuote);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}