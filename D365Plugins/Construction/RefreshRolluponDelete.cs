using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class RefreshRolluponDeletePreEvent : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is EntityReference && context.Depth == 1)
            {
                EntityReference e = (EntityReference)context.InputParameters["Target"];
                Entity entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));

                try
                {
                    var constructionRate = organizationService.Retrieve("lux_constructiontechnicalrate", entity.Id, new ColumnSet(true));
                    var constructionQuote = organizationService.Retrieve("lux_constructionquotes", constructionRate.GetAttributeValue<EntityReference>("lux_constructionquote").Id, new ColumnSet(true));
                    context.SharedVariables.Add("ConstructionQuote", constructionQuote.Id.ToString());
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }

    public class RefreshRolluponDeletePostEvent : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                if (context.SharedVariables.Contains("ConstructionQuote"))
                {
                    Guid ConstructionQuoteId = new Guid((string)context.SharedVariables["ConstructionQuote"]);
                    CalculateRollupFieldRequest request = new CalculateRollupFieldRequest
                    {
                        Target = new EntityReference("lux_constructionquotes", ConstructionQuoteId),
                        FieldName = "lux_overallcompositerate"// Rollup Field Name
                    };
                    CalculateRollupFieldResponse response = (CalculateRollupFieldResponse)organizationService.Execute(request);

                    CalculateRollupFieldRequest request1 = new CalculateRollupFieldRequest
                    {
                        Target = new EntityReference("lux_constructionquotes", ConstructionQuoteId),
                        FieldName = "lux_overallcompositerate1"// Rollup Field Name
                    };
                    CalculateRollupFieldResponse response1 = (CalculateRollupFieldResponse)organizationService.Execute(request1);
                }

            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}