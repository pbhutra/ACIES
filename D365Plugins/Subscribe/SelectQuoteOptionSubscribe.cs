using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class SelectQuoteOptionSubscribe : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var subsQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", entity.Id, new ColumnSet(true));
                    var SelectedQuoteOption = subsQuoteOption.Attributes.Contains("lux_quoteoptionselected") ? subsQuoteOption.GetAttributeValue<bool>("lux_quoteoptionselected") : false;

                    var subsQuote = organizationService.Retrieve("lux_subscribepiquote", subsQuoteOption.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(false));
                    subsQuote["lux_quoteoptions"] = new EntityReference("lux_subscribequoteoption", subsQuoteOption.Id);
                    organizationService.Update(subsQuote);

                    if (SelectedQuoteOption == true)
                    {
                        var OptionsFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribequoteoption'>
                                                <attribute name='lux_subscribequoteoptionid' />
                                                <attribute name='lux_name' />
                                                <attribute name='createdon' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                  <condition attribute='lux_subscribequoteoptionid' operator='ne' uiname='' uitype='lux_subscribequoteoption' value='{subsQuoteOption.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                        var optionsList = organizationService.RetrieveMultiple(new FetchExpression(OptionsFetch));
                        if (optionsList.Entities.Count() > 0)
                        {
                            foreach (var item in optionsList.Entities)
                            {
                                item["lux_quoteoptionselected"] = false;
                                organizationService.Update(item);
                            }
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