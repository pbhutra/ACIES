using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Net;
using System.ServiceModel.Description;

namespace ACIES
{
    public class GetBrokerTotalAmountDues : CodeActivity
    {
        [Input("Account")]
        [ReferenceTarget("account")]
        public InArgument<EntityReference> Account { get; set; }

        [Output("Amount DueThisMonth")]
        public OutArgument<Money> AmountDueThisMonth { get; set; }

        [Output("Amount OverDue")]
        public OutArgument<Money> AmountOverDue { get; set; }

        [Output("AXA Amount DueThisMonth")]
        public OutArgument<Money> AXAAmountDueThisMonth { get; set; }

        [Output("AXA Amount OverDue")]
        public OutArgument<Money> AXAAmountOverDue { get; set; }

        [Output("Argenta Amount DueThisMonth")]
        public OutArgument<Money> ArgentaAmountDueThisMonth { get; set; }

        [Output("Argenta Amount OverDue")]
        public OutArgument<Money> ArgentaAmountOverDue { get; set; }

        [Output("Recruitment Amount DueThisMonth")]
        public OutArgument<Money> RecruitmentAmountDueThisMonth { get; set; }

        [Output("Recruitment Amount OverDue")]
        public OutArgument<Money> RecruitmentAmountOverDue { get; set; }

        [Output("PT Amount DueThisMonth")]
        public OutArgument<Money> PTAmountDueThisMonth { get; set; }

        [Output("PT Amount OverDue")]
        public OutArgument<Money> PTAmountOverDue { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            DateTime d = DateTime.Now.CurrentMonthFirstDay();
            var date = new DateTime(d.Year, d.Month, 1).AddDays(-1);

            var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='lux_invoice'>
                                    <attribute name='lux_transactiontype' />
                                    <attribute name='lux_transactiondate' />
                                    <attribute name='lux_totalpremiumincludingiptfee' />
                                    <attribute name='lux_risktransaction' />
                                    <attribute name='lux_policynumber' />
                                    <attribute name='lux_netpremiumdueincludingiptfee' />
                                    <attribute name='lux_ipt' />
                                    <attribute name='lux_insured' />
                                    <attribute name='lux_inceptioneffectivedate' />
                                    <attribute name='lux_grosspremiumexciptfee' />
                                    <attribute name='lux_fee' />
                                    <attribute name='lux_duedate' />
                                    <attribute name='lux_commissionrate' />
                                    <attribute name='lux_commissionamount' />
                                    <attribute name='lux_mgatype' />
                                    <attribute name='lux_invoiceid' />
                                    <order attribute='lux_transactiondate' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='statecode' operator='eq' value='0' />
                                      <condition attribute='lux_broker' operator='eq' uiname='' uitype='account' value='{Account.Get(executionContext).Id}' />
                                      <condition attribute='lux_ispaid' operator='ne' value='1' />
                                      <condition attribute='lux_duedate' operator='this-month' />
                                    </filter>
                                  </entity>
                                </fetch>";

            if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
            {

                var amount = service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970001).Sum(x => (x.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? x.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0));
                AmountDueThisMonth.Set(executionContext, new Money(amount));

                var amount1 = service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970002).Sum(x => (x.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? x.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0));
                AXAAmountDueThisMonth.Set(executionContext, new Money(amount1));

                var amount2 = service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970003).Sum(x => (x.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? x.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0));
                ArgentaAmountDueThisMonth.Set(executionContext, new Money(amount2));

                var amount3 = service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970004).Sum(x => (x.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? x.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0));
                RecruitmentAmountDueThisMonth.Set(executionContext, new Money(amount3));

                var amount4 = service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970005).Sum(x => (x.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? x.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0));
                PTAmountDueThisMonth.Set(executionContext, new Money(amount4));
            }
            else
            {
                AmountDueThisMonth.Set(executionContext, new Money(0));
                AXAAmountDueThisMonth.Set(executionContext, new Money(0));
                ArgentaAmountDueThisMonth.Set(executionContext, new Money(0));
                RecruitmentAmountDueThisMonth.Set(executionContext, new Money(0));
                PTAmountDueThisMonth.Set(executionContext, new Money(0));
            }

            var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='lux_invoice'>
                                    <attribute name='lux_transactiontype' />
                                    <attribute name='lux_transactiondate' />
                                    <attribute name='lux_totalpremiumincludingiptfee' />
                                    <attribute name='lux_risktransaction' />
                                    <attribute name='lux_policynumber' />
                                    <attribute name='lux_netpremiumdueincludingiptfee' />
                                    <attribute name='lux_ipt' />
                                    <attribute name='lux_insured' />
                                    <attribute name='lux_inceptioneffectivedate' />
                                    <attribute name='lux_grosspremiumexciptfee' />
                                    <attribute name='lux_fee' />
                                    <attribute name='lux_duedate' />
                                    <attribute name='lux_commissionrate' />
                                    <attribute name='lux_commissionamount' />
                                    <attribute name='lux_mgatype' />
                                    <attribute name='lux_invoiceid' />
                                    <order attribute='lux_transactiondate' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='statecode' operator='eq' value='0' />
                                      <condition attribute='lux_broker' operator='eq' uiname='' uitype='account' value='{Account.Get(executionContext).Id}' />
                                      <condition attribute='lux_ispaid' operator='ne' value='1' />
                                      <condition attribute='lux_duedate' operator='on-or-before' value='{date}' />
                                    </filter>
                                  </entity>
                                </fetch>";

            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
            {
                var DueAmount = 0.00M;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970001))
                {
                    item["lux_ispremiumdue"] = true;
                    service.Update(item);
                    DueAmount += item.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? item.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0.00M;
                }
                AmountOverDue.Set(executionContext, new Money(DueAmount));


                var DueAmount1 = 0.00M;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970002))
                {
                    item["lux_ispremiumdueaxa"] = true;
                    service.Update(item);
                    DueAmount1 += item.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? item.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0.00M;
                }
                AXAAmountOverDue.Set(executionContext, new Money(DueAmount1));


                var DueAmount2 = 0.00M;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970003))
                {
                    item["lux_ispremiumdueargenta"] = true;
                    service.Update(item);
                    DueAmount2 += item.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? item.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0.00M;
                }
                ArgentaAmountOverDue.Set(executionContext, new Money(DueAmount2));

                var DueAmount3 = 0.00M;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970004))
                {
                    item["lux_ispremiumduerecruitment"] = true;
                    service.Update(item);
                    DueAmount3 += item.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? item.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0.00M;
                }
                RecruitmentAmountOverDue.Set(executionContext, new Money(DueAmount3));

                var DueAmount4 = 0.00M;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Where(a => a.GetAttributeValue<OptionSetValue>("lux_mgatype").Value == 972970005))
                {
                    item["lux_ispremiumdueportsandterminals"] = true;
                    service.Update(item);
                    DueAmount4 += item.Attributes.Contains("lux_netpremiumdueincludingiptfee") ? item.GetAttributeValue<Money>("lux_netpremiumdueincludingiptfee").Value : 0.00M;
                }
                PTAmountOverDue.Set(executionContext, new Money(DueAmount4));
            }
            else
            {
                AmountOverDue.Set(executionContext, new Money(0));
                AXAAmountOverDue.Set(executionContext, new Money(0));
                ArgentaAmountOverDue.Set(executionContext, new Money(0));
                RecruitmentAmountOverDue.Set(executionContext, new Money(0));
                PTAmountOverDue.Set(executionContext, new Money(0));
            }
        }
    }
}
