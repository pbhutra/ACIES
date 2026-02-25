using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class ACIESIntangicPremium : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // The InputParameters collection contains all the data
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var intangicproduct = organizationService.Retrieve("lux_intangicproduct", entity.Id, new ColumnSet(true));

                    int relativeBenchmark = intangicproduct.GetAttributeValue<OptionSetValue>("lux_relativebenchmark").Value;
                    var frequencyAnaysis = intangicproduct.GetAttributeValue<EntityReference>("lux_frequencyanalysis");
                    var cbhSector = intangicproduct.GetAttributeValue<EntityReference>("lux_cbhsector");
                    var GiscCat = intangicproduct.GetAttributeValue<EntityReference>("lux_insuredgicscategorisation");

                    var versionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_intangicquoteversion'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='createdon' />
                                                            <attribute name='lux_totalnetpremium' />
                                                            <attribute name='lux_commission' />
                                                            <attribute name='lux_relativebenchmark' />
                                                            <attribute name='lux_sectorscore_6ma' />
                                                            <attribute name='lux_frequencyanalysis' />
                                                            <attribute name='lux_insuredgicscategorisation' />
                                                            <attribute name='lux_cbhsector' />
                                                            <attribute name='lux_technicalselectedrate_15' />
                                                            <attribute name='lux_technicalselectedrate_20' />
                                                            <attribute name='lux_technicalselectedrate_25' />
                                                            <attribute name='lux_technicalselectedrate_30' />
                                                            <attribute name='lux_technicalselectedrate_35' />
                                                            <attribute name='lux_intangicquoteversionid' />
                                                            <order attribute='createdon' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_intangicquote' operator='eq' uiname='' uitype='lux_intangicproduct' value='{intangicproduct.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    var versionList = organizationService.RetrieveMultiple(new FetchExpression(versionFetch));

                    if (versionList.Entities.Count() > 0)
                    {
                        foreach (var item in versionList.Entities)
                        {
                            item["lux_relativebenchmark"] = new OptionSetValue(relativeBenchmark);
                            item["lux_sectorscore_6ma"] = intangicproduct.GetAttributeValue<decimal>("lux_sectorscore_6ma");
                            item["lux_frequencyanalysis"] = new EntityReference(frequencyAnaysis.LogicalName, frequencyAnaysis.Id);
                            item["lux_insuredgicscategorisation"] = new EntityReference(GiscCat.LogicalName, GiscCat.Id);
                            item["lux_cbhsector"] = new EntityReference(cbhSector.LogicalName, cbhSector.Id);
                            item["lux_technicalselectedrate_15"] = null;
                            item["lux_technicalselectedrate_20"] = null;
                            item["lux_technicalselectedrate_25"] = null;
                            item["lux_technicalselectedrate_30"] = null;
                            item["lux_technicalselectedrate_35"] = null;
                            organizationService.Update(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }
            }
        }
    }
}
