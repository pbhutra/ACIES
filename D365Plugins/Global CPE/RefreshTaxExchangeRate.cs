using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class RefreshTaxExchangeRate : IPlugin
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

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_cpequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_cpequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id}' />
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
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{PremiumRow.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id}' />
                                                      <condition attribute='lux_cpequotetaxtypeid' operator='ne' uiname='' uitype='lux_cpequotetaxtype' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    }

                    var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));

                    throw new InvalidPluginExecutionException(ptList.Entities.Count.ToString());

                    if (ptList.Entities.Count() > 0)
                    {
                        Entity application = organizationService.Retrieve("lux_cpequotetaxtype", entity.Id, new ColumnSet(false));
                        application["lux_currencyrefreshfield"] = new Money(new Random().Next(int.MinValue, int.MaxValue));
                        organizationService.Update(application);
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