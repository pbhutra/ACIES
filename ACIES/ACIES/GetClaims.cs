using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACIES
{
    public class GetClaims : CodeActivity
    {
        [RequiredArgument]
        [Input("PolicyNumber")]
        public InArgument<string> PolicyNumber { get; set; }

        [Output("Claim")]
        [ReferenceTarget("lux_claim")]
        public OutArgument<EntityReference> Claim { get; set; }

        [Output("IsPolicyExist")]
        public OutArgument<bool> IsPolicyExist { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                <entity name='lux_claim'>
                                    <attribute name='lux_claimid' />
                                    <attribute name='lux_name' />
                                    <attribute name='createdon' />
                                    <order attribute='lux_name' descending='false' />
                                    <link-entity name='lux_policy' from='lux_policyid' to='lux_policy' link-type='inner'>
                                          <filter type='and'>
                                            <condition attribute='lux_policynumber' operator='eq' value='{PolicyNumber.Get(executionContext).ToString()}' />
                                          </filter>
                                        </link-entity>
                                </entity>
                        </fetch>";

            var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_policy'>
                                <attribute name='createdon' />
                                <attribute name='lux_product' />
                                <attribute name='lux_policyholder' />
                                <attribute name='lux_policystartdate' />
                                <attribute name='lux_netpremium' />
                                <attribute name='lux_grosspremium' />
                                <attribute name='lux_brokercompany' />
                                <attribute name='lux_name' />
                                <attribute name='lux_policynumber' />
                                <attribute name='lux_policyid' />
                                <order attribute='createdon' descending='true' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_policynumber' operator='eq' value='{PolicyNumber.Get(executionContext).ToString()}' />
                                </filter>
                              </entity>
                            </fetch>";

            if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
            {
                var claim = service.RetrieveMultiple(new FetchExpression(fetch)).Entities[0];
                Claim.Set(executionContext, claim.ToEntityReference());
            }

            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
            {
                IsPolicyExist.Set(executionContext, true);
            }
            else
            {
                IsPolicyExist.Set(executionContext, false);
            }
        }
    }
}
