using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ACIES
{
    public class GetCurrentMonthFirstAndLastDay : CodeActivity
    {
        [RequiredArgument]
        [Output("CurrentMonth First Day")]
        public OutArgument<DateTime> CurrentMonthFirstDay { get; set; }

        [RequiredArgument]
        [Output("CurrentMonth Last Day")]
        public OutArgument<DateTime> CurrentMonthLastDay { get; set; }

        [RequiredArgument]
        [Output("PreviousMonth Name")]
        public OutArgument<string> PreviousMonthName { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            DateTime t = DateTime.Now;

            DateTime p = t.CurrentMonthFirstDay();
            CurrentMonthFirstDay.Set(executionContext, p);

            p = t.CurrentMonthLastDay();
            CurrentMonthLastDay.Set(executionContext, p);

            PreviousMonthName.Set(executionContext, p.AddMonths(-1).ToString("MMMM") + " " + p.AddMonths(-1).Year.ToString());
        }
    }

    public static class Helpers1
    {
        public static DateTime CurrentMonthFirstDay(this DateTime currentDate)
        {
            DateTime d = currentDate.CurrentMonthLastDay();
            return new DateTime(d.Year, d.Month, 1);
        }

        public static DateTime CurrentMonthLastDay(this DateTime currentDate)
        {
            if (currentDate.Month < 12)
                return new DateTime(currentDate.Year, currentDate.Month + 1, 1).AddDays(-1);
            else
                return new DateTime(currentDate.Year + 1, 1, 1).AddDays(-1);
        }
    }
}
