using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Plugins
{
    public class RecruitmentQuotePremium_CalculateMTAPolicyPremium : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            tracingService.Trace("CalculateMTAPolicyPremium execution started.");

            try
            {
                // Validate the target
                if (!(context.InputParameters["Target"] is Entity target) || target.LogicalName != "lux_specialistschemerecruitmentpremuim")
                    return;

                string messageName = context.MessageName.ToLower();

                if ((messageName != "create" && messageName != "update") || context.Stage != 20)
                    return;

                int applicationType = 0;
                EntityReference quoteRef = null;

                if (target.Contains("lux_recruitmentquote"))
                {
                    quoteRef = target.GetAttributeValue<EntityReference>("lux_recruitmentquote");
                }
                else if (context.PreEntityImages.Contains("PreImage") && context.PreEntityImages["PreImage"].Contains("lux_recruitmentquote"))
                {
                    quoteRef = context.PreEntityImages["PreImage"].GetAttributeValue<EntityReference>("lux_recruitmentquote");
                }

                //if (quoteRef == null || quoteRef.Id == Guid.Empty)
                //    return;

                ColumnSet cols = new ColumnSet("lux_applicationtype");
                Entity recruitmentQuote = service.Retrieve("lux_recruitmentquotes", quoteRef.Id, cols);

                if (recruitmentQuote != null && recruitmentQuote.Contains("lux_applicationtype"))
                {
                    applicationType = recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;
                }

                //MTA
                if (applicationType == 972970002)
                {
                    decimal technicalPremium = 0M, loadingDiscount = 0M;

                    if (target.Contains("lux_mtatechnicalpremium"))
                    {
                        if (target["lux_mtatechnicalpremium"] != null)
                        {
                            technicalPremium = target.GetAttributeValue<Money>("lux_mtatechnicalpremium").Value;
                        }
                    }
                    else if (context.PreEntityImages.Contains("PreImage") && context.PreEntityImages["PreImage"].Contains("lux_mtatechnicalpremium"))
                    {
                        technicalPremium = context.PreEntityImages["PreImage"].GetAttributeValue<Money>("lux_mtatechnicalpremium").Value;
                    }

                    if (target.Contains("lux_loaddiscount"))
                    {
                        if (target["lux_loaddiscount"] != null)
                        {
                            loadingDiscount = target.GetAttributeValue<decimal>("lux_loaddiscount");
                        }
                    }
                    else if (context.PreEntityImages.Contains("PreImage") && context.PreEntityImages["PreImage"].Contains("lux_loaddiscount"))
                    {
                        loadingDiscount = context.PreEntityImages["PreImage"].GetAttributeValue<decimal>("lux_loaddiscount");
                    }

                    decimal policyPremium = technicalPremium + ((technicalPremium * loadingDiscount) / 100);

                    target["lux_mtapolicypremium"] = new Money(policyPremium);
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace($"Business Rule Exception: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Unexpected Exception: {ex}");
                throw new InvalidPluginExecutionException("Plugin failed: " + ex.Message, ex);
            }
        }
    }
}
