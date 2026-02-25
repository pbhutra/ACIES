using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateMPLOurShare : IPlugin
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

                    var constructionQuote = organizationService.Retrieve("lux_constructionquotes", entity.Id, new ColumnSet(true));
                    var PolicyType = constructionQuote.FormattedValues["lux_typeofpolicy2"].ToString();

                    var MPLOurShare = 0M;
                    var MaximumContractLimit = constructionQuote.Attributes.Contains("lux_maximumcontractlimit") ? constructionQuote.GetAttributeValue<Money>("lux_maximumcontractlimit").Value : 0;

                    if (PolicyType == "CAR single project - CR")
                    {
                        MaximumContractLimit = constructionQuote.Attributes.Contains("lux_estimatedcontractvalue") ? constructionQuote.GetAttributeValue<Money>("lux_estimatedcontractvalue").Value : 0;
                    }

                    var Works = MaximumContractLimit * 1.25M;
                    var OwnPlantAnyOne = constructionQuote.Attributes.Contains("lux_ownplantanyoneoccurrence") ? constructionQuote.GetAttributeValue<Money>("lux_ownplantanyoneoccurrence").Value : 0;
                    var HIPAnyOne = constructionQuote.Attributes.Contains("lux_hiredinplantlimit") ? constructionQuote.GetAttributeValue<Money>("lux_hiredinplantlimit").Value : 0;
                    var TempBuildingAnyOne = constructionQuote.Attributes.Contains("lux_temporarybuildingsanyoneoccurrence") ? constructionQuote.GetAttributeValue<Money>("lux_temporarybuildingsanyoneoccurrence").Value : 0;
                    var EmployeeToolAnyOne = constructionQuote.Attributes.Contains("lux_employeestoolsanyoneoccurrence") ? constructionQuote.GetAttributeValue<Money>("lux_employeestoolsanyoneoccurrence").Value : 0;
                    var OtherItemsAnyOne = constructionQuote.Attributes.Contains("lux_otheritemsanyoneoccurrence") ? constructionQuote.GetAttributeValue<Money>("lux_otheritemsanyoneoccurrence").Value : 0;

                    var AnnualExistingStructure = 0M;

                    var PhoenixShareQuoted = constructionQuote.Attributes.Contains("lux_phoenixsharequoted") ? constructionQuote.GetAttributeValue<decimal>("lux_phoenixsharequoted") : 0M;

                    if (constructionQuote.Attributes.Contains("lux_existingstructure") && constructionQuote.GetAttributeValue<bool>("lux_existingstructure") == true)
                    {
                        if (constructionQuote.Attributes.Contains("lux_existingstructures") && constructionQuote.GetAttributeValue<OptionSetValue>("lux_existingstructures").Value == 972970001)//Sublimit
                        {
                            AnnualExistingStructure = constructionQuote.Attributes.Contains("lux_sublimit") ? constructionQuote.GetAttributeValue<Money>("lux_sublimit").Value : 0M;
                        }
                        else if (constructionQuote.Attributes.Contains("lux_existingstructures") && constructionQuote.GetAttributeValue<OptionSetValue>("lux_existingstructures").Value == 972970002)//Full Cover
                        {
                            AnnualExistingStructure = constructionQuote.Attributes.Contains("lux_existingstructuretsi") ? constructionQuote.GetAttributeValue<Money>("lux_existingstructuretsi").Value : 0M;
                        }
                    }

                    var PropertiesAndContentSI = constructionQuote.Attributes.Contains("lux_propertiesandcontentssuminsured") ? constructionQuote.GetAttributeValue<Money>("lux_propertiesandcontentssuminsured").Value : 0M;
                    var Authorities = MaximumContractLimit * 0.1M;
                    var DebrisRemoval = MaximumContractLimit * 0.1M;
                    var Plans = constructionQuote.Attributes.Contains("lux_plans") ? constructionQuote.GetAttributeValue<Money>("lux_plans").Value : 0;

                    var ProfessionalFees = 1000000M;
                    if (MaximumContractLimit * 0.15M < 1000000)
                    {
                        ProfessionalFees = MaximumContractLimit * 0.15M;
                    }

                    var ICOW = MaximumContractLimit * 0.1M;

                    var SecurityDevicesCover = constructionQuote.Attributes.Contains("lux_securitydevicescover") ? constructionQuote.GetAttributeValue<Money>("lux_securitydevicescover").Value : 0;
                    var FireBrigadeCharges = constructionQuote.Attributes.Contains("lux_firebrigadecharges") ? constructionQuote.GetAttributeValue<Money>("lux_firebrigadecharges").Value : 0;
                    var ImmenientDamageAvoidance = constructionQuote.Attributes.Contains("lux_immenientdamageavoidance") ? constructionQuote.GetAttributeValue<Money>("lux_immenientdamageavoidance").Value : 0;
                    var HIPLegalDefence = HIPAnyOne * 0.1M;
                    var ImmobilisedPlantCover = constructionQuote.Attributes.Contains("lux_immobilisedplantcover") ? constructionQuote.GetAttributeValue<Money>("lux_immobilisedplantcover").Value : 0;
                    var LossOfLeys = constructionQuote.Attributes.Contains("lux_imminentdamageavoidancecosts") ? constructionQuote.GetAttributeValue<Money>("lux_imminentdamageavoidancecosts").Value : 0;
                    var AdditionalCost = constructionQuote.Attributes.Contains("lux_additionalcostsupplementaryexpenses") ? constructionQuote.GetAttributeValue<Money>("lux_additionalcostsupplementaryexpenses").Value : 0;
                    var SignwritingAndLivery = constructionQuote.Attributes.Contains("lux_signwritingandlivery") ? constructionQuote.GetAttributeValue<Money>("lux_signwritingandlivery").Value : 0;
                    var IncidentalHireOfPlant = constructionQuote.Attributes.Contains("lux_incidentalhireofplant") ? constructionQuote.GetAttributeValue<Money>("lux_incidentalhireofplant").Value : 0;
                    var PlantOnFreeLoan = constructionQuote.Attributes.Contains("lux_plantonfreeloan") ? constructionQuote.GetAttributeValue<Money>("lux_plantonfreeloan").Value : 0;
                    var ReplacementHireCharges = constructionQuote.Attributes.Contains("lux_replacementhirecharges") ? constructionQuote.GetAttributeValue<Money>("lux_replacementhirecharges").Value : 0;
                    var NonFerrous = constructionQuote.GetAttributeValue<bool>("lux_offsitestorageanyoneperiodofinsurance") == true ? (constructionQuote.Attributes.Contains("lux_offsitestoragenonferrousmetals") ? constructionQuote.GetAttributeValue<Money>("lux_offsitestoragenonferrousmetals").Value : 0) : 0;
                    var AllOther = constructionQuote.GetAttributeValue<bool>("lux_offsitestorageanyoneperiodofinsurance") == true ? (constructionQuote.Attributes.Contains("lux_offsitestorageallothermaterials") ? constructionQuote.GetAttributeValue<Money>("lux_offsitestorageallothermaterials").Value : 0) : 0;
                    var DSUSI = constructionQuote.GetAttributeValue<bool>("lux_dilayinstartupcoverrequired") == true ? (constructionQuote.Attributes.Contains("lux_totaltsi") ? constructionQuote.GetAttributeValue<Money>("lux_totaltsi").Value : 0) : 0;

                    if (PolicyType == "CAR annual programme - CA")
                    {
                        MPLOurShare = Works + OwnPlantAnyOne + HIPAnyOne + TempBuildingAnyOne + EmployeeToolAnyOne + OtherItemsAnyOne + AnnualExistingStructure + PropertiesAndContentSI + Authorities + DebrisRemoval + Plans + ProfessionalFees + ICOW + SecurityDevicesCover + FireBrigadeCharges + ImmenientDamageAvoidance + NonFerrous + AllOther;
                    }
                    else if (PolicyType == "CPE annual programme - CP")
                    {
                        MPLOurShare = OwnPlantAnyOne + HIPAnyOne + TempBuildingAnyOne + EmployeeToolAnyOne + OtherItemsAnyOne + FireBrigadeCharges + HIPLegalDefence + ImmenientDamageAvoidance + ImmobilisedPlantCover + LossOfLeys + SecurityDevicesCover + AdditionalCost + SignwritingAndLivery + IncidentalHireOfPlant + PlantOnFreeLoan + ReplacementHireCharges;
                    }
                    else if (PolicyType == "CAR single project - CR")
                    {
                        MPLOurShare = Works + OwnPlantAnyOne + HIPAnyOne + TempBuildingAnyOne + EmployeeToolAnyOne + OtherItemsAnyOne + AnnualExistingStructure + PropertiesAndContentSI + Authorities + DebrisRemoval + Plans + ProfessionalFees + ICOW + SecurityDevicesCover + FireBrigadeCharges + ImmenientDamageAvoidance + DSUSI + NonFerrous + AllOther;
                    }

                    constructionQuote["lux_mplourshare"] = MPLOurShare;
                    organizationService.Update(constructionQuote);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}