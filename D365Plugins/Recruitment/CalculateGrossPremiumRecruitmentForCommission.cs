using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateGrossPremiumRecruitmentForCommission : IPlugin
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

                    var recruitmentQuote = organizationService.Retrieve("lux_recruitmentquotes", entity.Id, new ColumnSet(true));

                    var TechnicalBrokerCommission = recruitmentQuote.Attributes.Contains("lux_technicalbrokercommission") ? recruitmentQuote.GetAttributeValue<decimal>("lux_technicalbrokercommission") : 25M;
                    var TechnicalACIESCommission = recruitmentQuote.Attributes.Contains("lux_technicalaciescommission") ? recruitmentQuote.GetAttributeValue<decimal>("lux_technicalaciescommission") : 7.5M;

                    var TotalTechnicalCommission = TechnicalBrokerCommission + TechnicalACIESCommission;

                    var PolicyBrokerCommission = recruitmentQuote.Attributes.Contains("lux_policybrokercommission") ? recruitmentQuote.GetAttributeValue<decimal>("lux_policybrokercommission") : 25M;
                    var PolicyACIESCommission = recruitmentQuote.Attributes.Contains("lux_policyaciescommission") ? recruitmentQuote.GetAttributeValue<decimal>("lux_policyaciescommission") : 7.5M;

                    var TotalPolicyCommission = PolicyBrokerCommission + PolicyACIESCommission;

                    //throw new InvalidPluginExecutionException(recruitmentQuote.Id.ToString());

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_specialistschemerecruitmentpremuim'>
                                                <attribute name='lux_name' />
                                                <attribute name='lux_section' />
                                                <attribute name='lux_recruitmentquote' />
                                                <attribute name='lux_technicalpremium' />
                                                <attribute name='lux_policypremium' />
                                                <attribute name='transactioncurrencyid' />
                                                <attribute name='lux_specialistschemerecruitmentpremuimid' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                                                  <condition attribute='lux_section' operator='in'>
                                                    <value>972970012</value>
                                                    <value>972970013</value>
                                                    <value>972970023</value>
                                                    <value>972970024</value>
                                                    <value>972970025</value>
                                                    <value>972970026</value>
                                                  </condition>
                                                </filter>
                                              </entity>
                                            </fetch>";

                    //var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                    //                          <entity name='lux_specialistschemerecruitmentpremuim'>
                    //                            <attribute name='lux_name' />
                    //                            <attribute name='lux_section' />
                    //                            <attribute name='lux_recruitmentquote' />
                    //                            <attribute name='lux_technicalpremium' />
                    //                            <attribute name='lux_policypremium' />
                    //                            <attribute name='transactioncurrencyid' />
                    //                            <attribute name='lux_specialistschemerecruitmentpremuimid' />
                    //                            <order attribute='lux_name' descending='false' />
                    //                            <filter type='and'>
                    //                              <condition attribute='statecode' operator='eq' value='0' />
                    //                              <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                    //                            </filter>
                    //                          </entity>
                    //                        </fetch>";

                    var recruitmentList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (recruitmentList.Entities.Count() > 0)
                    {
                        var TechnicalPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                        var PolicyPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);
                        var LESection = recruitmentList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970023);
                        var LETechnicalPremium = LESection.Attributes.Contains("lux_technicalpremium") ? LESection.GetAttributeValue<Money>("lux_technicalpremium").Value : 0;
                        var LEPolicyPremium = LESection.Attributes.Contains("lux_policypremium") ? LESection.GetAttributeValue<Money>("lux_policypremium").Value : 0;

                        decimal Fee = 0;
                        decimal PolicyFee = 0;

                        var TechnicalFeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{TechnicalPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{TechnicalPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";

                        if (organizationService.RetrieveMultiple(new FetchExpression(TechnicalFeeFetch)).Entities.Count > 0)
                        {
                            Fee = organizationService.RetrieveMultiple(new FetchExpression(TechnicalFeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                        }

                        var PolicyFeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{PolicyPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{PolicyPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";

                        if (organizationService.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities.Count > 0)
                        {
                            PolicyFee = organizationService.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                        }

                        Entity application = organizationService.Retrieve("lux_recruitmentquotes", recruitmentQuote.Id, new ColumnSet(false));
                        application["lux_technicalpremiumbeforetax"] = new Money((TechnicalPremium - LETechnicalPremium) * 0.675M / (1 - TotalTechnicalCommission / 100));
                        application["lux_policypremiumbeforetax"] = new Money((PolicyPremium - LEPolicyPremium) * 0.675M / (1 - TotalPolicyCommission / 100));

                        application["lux_technicalpremiumbeforetaxexclegal"] = new Money((TechnicalPremium - LETechnicalPremium) * 0.675M / (1 - TotalTechnicalCommission / 100));
                        application["lux_policypremiumbeforetaxexclegal"] = new Money((PolicyPremium - LEPolicyPremium) * 0.675M / (1 - TotalPolicyCommission / 100));

                        application["lux_technicalpolicyfee"] = new Money(Fee);
                        application["lux_policypolicyfee"] = new Money(PolicyFee);

                        application["lux_letechnicalpremiumexcle"] = new Money(0);
                        application["lux_lepolicypremiumexcle"] = new Money(0);

                        if (LESection != null)
                        {
                            application["lux_technicallegalpremiumbeforetaxinc"] = new Money(LETechnicalPremium * 0.675M / (1 - TotalTechnicalCommission / 100));
                            application["lux_lepolicypremium"] = new Money(LEPolicyPremium * 0.675M / (1 - TotalPolicyCommission / 100));
                        }
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