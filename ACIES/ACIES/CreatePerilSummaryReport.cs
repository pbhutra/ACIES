using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace ACIES
{
    public class CreatePerilSummaryReport : CodeActivity
    {
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var premisefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_perilreport'>
                                        <attribute name='lux_street' />
                                        <attribute name='lux_riskpostcode' />
                                        <attribute name='lux_policynumber' />
                                        <attribute name='lux_locationnumber' />
                                        <attribute name='lux_insuredname' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_floodcover' />
                                        <attribute name='lux_endorsement' />
                                        <attribute name='lux_approvedeclinecomment' />
                                        <attribute name='lux_perilreportid' />
                                        <order attribute='lux_street' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                        </filter>
                                        <link-entity name='lux_policy' from='lux_policyid' to='lux_policy' link-type='inner' alias='ab'>
                                              <filter type='and'>
                                                <condition attribute='statuscode' operator='in'>
                                                  <value>972970004</value>
                                                  <value>972970005</value>
                                                  <value>972970000</value>
                                                  <value>1</value>
                                                  <value>972970002</value>
                                                  <value>972970006</value>
                                                </condition>
                                              </filter>
                                            </link-entity>
                                      </entity>
                                    </fetch>";

            var premises = service.RetrieveMultiple(new FetchExpression(premisefetch)).Entities;
            foreach (var flood in premises.GroupBy(x => x.Attributes["lux_floodscore"]))
            {
                var floodScore = flood.FirstOrDefault().Attributes.Contains("lux_floodscore") ? Convert.ToInt32(flood.FirstOrDefault().Attributes["lux_floodscore"]) : 0;
                var perilfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_perilsummary'>
                                            <attribute name='lux_perilsummaryid' />
                                            <attribute name='lux_name' />
                                            <attribute name='createdon' />
                                            <order attribute='lux_name' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_floodzone' operator='eq' value='{floodScore}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                var floods = service.RetrieveMultiple(new FetchExpression(perilfetch)).Entities;
                if (floods.Count() > 0)
                {
                    Entity peril = new Entity("lux_perilsummary", floods.FirstOrDefault().Id);
                    peril["lux_floodzone"] = floodScore;
                    peril["lux_numberofproperties"] = flood.Count();
                    service.Update(peril);
                }
                else
                {
                    Entity peril = new Entity("lux_perilsummary");
                    peril["lux_floodzone"] = floodScore;
                    peril["lux_numberofproperties"] = flood.Count();
                    service.Create(peril);
                }
            }
        }
    }
}
