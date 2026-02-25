using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class GetMajorShareholderSubscribe : IPlugin
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
                    var cpeQuote = organizationService.Retrieve("lux_subscribemlriskinfo", TradeRow.GetAttributeValue<EntityReference>("lux_subscribemlriskinfo").Id, new ColumnSet(false));

                    var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribemajorshareholder'>
                                            <attribute name='lux_name' />
                                            <attribute name='createdon' />
                                            <attribute name='lux_amountofshareholding' />
                                            <attribute name='lux_subscribemajorshareholderid' />
                                            <order attribute='lux_name' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribemlriskinfo' operator='eq' uiname='' uitype='lux_subscribemlriskinfo' value='{cpeQuote.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                    if (context.MessageName == "Delete")
                    {
                        tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribemajorshareholder'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_amountofshareholding' />
                                                    <attribute name='lux_subscribemajorshareholderid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribemlriskinfo' operator='eq' uiname='' uitype='lux_subscribemlriskinfo' value='{cpeQuote.Id}' />
                                                      <condition attribute='lux_subscribemajorshareholderid' operator='ne' uiname='' uitype='lux_subscribemajorshareholder' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    }

                    var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));

                    decimal TotalPercentage = tradeList.Entities.Sum(x => x.Attributes.Contains("lux_amountofshareholding") ? x.GetAttributeValue<decimal>("lux_amountofshareholding") : 0M);

                    if (TotalPercentage > 100)
                    {
                        throw new InvalidPluginExecutionException("Total Shareholding Percentage should not exceed 100");
                    }

                    //cpeQuote["lux_activitiesdctotalpercentage"] = TotalPercentage;
                    //organizationService.Update(cpeQuote);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}