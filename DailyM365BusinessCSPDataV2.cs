using Microsoft.SharePoint.Client;      // ClientContext
using System;                           // DateTime, Environment, String
using System.Collections.Generic;       // List<T>
using System.Data;                      // DataRow, DataTable
using System.Linq;                      // IEnumerable<T>.Where, IEnumerable<T>.Select, IEnumerable<T>.Count, IEnumerable<T>.First (Extension methods)
using System.IO;                        // Path, File, FileStream, StreamReader, StreamWriter
using System.Net;                       // WebClient
using System.Security;                  // SecureString
using System.Text;                      // Encoding
using ScopeClient;                      // Scope, SubmitParameters
using VcClient;                         // JobInfo, VC

using static System.Configuration.ConfigurationManager; // AppSettings
using static System.Console;                            // Write, WriteLine, ReadKey
using static System.Diagnostics.Process;                // Start
using static System.Environment;                        // CurrentDirectory, GetFolderPath, Exit
using static System.Threading.Thread;                   // Sleep

namespace DailyM365BusinessCSPData
{
    class Program
    {

        #region Scope File Names and URL Paths...
        // From our scope.script verbatim (mostly) ^^
        private static int    days                 = Convert.ToInt32(AppSettings.Get("AddDays"));
        private static int    daysTenantInfo       = Convert.ToInt32(AppSettings.Get("AddDaysTenantInfo"));
        private static int    daysSetup            = Convert.ToInt32(AppSettings.Get("AddDaysSetup"));
        private static string filenamePreview      = "m365BusinessCspInfoV2Preview-"          + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".csv";
        private static string filenameProduction   = "m365BusinessCspInfoV2Production-"       + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".csv";
        private static string filenameBusiness     = "m365BusinessCspInfoV2-"                 + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".Business.TenantIds.csv";
        private static string filenameBusinessEss  = "m365BusinessCspInfoV2-"                 + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".Business.Essentials.TenantIds.csv";
        private static string filenameBusinessPrem = "m365BusinessCspInfoV2-"                 + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".Business.Premium.TenantIds.csv";
        private static string filenameEnterpriseE3 = "m365BusinessCspInfoV2-"                 + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".Enterprise.E3.TenantIds.csv";
        private static string filenameExchange     = "m365BusinessCspInfoV2-"                 + DateTime.Now.AddDays(days).ToString("yyyy-MM-dd")  + ".Exchange.Online.Essentials.TenantIds.csv";
        private static string filenameCombinedProd = "m365bSignUpAndTenantInfo-Production.'"  + DateTime.Now.AddDays(daysTenantInfo).ToString("yyyy-MM-dd") + "'.csv";
        private static string filenameCombinedPrev = "m365bSignUpAndTenantInfo-Preview.'"     + DateTime.Now.AddDays(daysTenantInfo).ToString("yyyy-MM-dd") + "'.csv";

        private static string urlPreview           = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenamePreview}";
        private static string urlProduction        = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameProduction}";
        private static string urlBusiness          = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameBusiness}";
        private static string urlBusinessEss       = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameBusinessEss}";
        private static string urlBusinessPrem      = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameBusinessPrem}";
        private static string urlEnterpriseE3      = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameEnterpriseE3}";
        private static string urlExhange           = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameExchange}";
        private static string urlCombinedProd      = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameCombinedProd}";
        private static string urlCombinedPrev      = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameCombinedPrev}";
        #endregion Scope File Names and URL Paths...

        private static DateTime dtday                      = DateTime.Now.AddDays(days);
        private static string year                         = dtday.ToString("yyyy");
        private static string month                        = dtday.ToString("MM");
        private static string day                          = dtday.ToString("dd");
        private static string sharePointFileNamePreview    = $"m365BusinessCSP.Preview.{year}.{month}.{day}.csv";
        private static string sharePointFileNameProduction = $"m365BusinessCSP.Production.{year}.{month}.{day}.csv";

        private static string localCSPDirectory            =  Path.Combine(GetFolderPath(SpecialFolder.MyDocuments), "COSMOS", "CSP");
        private static string localSetupDirectory          =  Path.Combine(GetFolderPath(SpecialFolder.MyDocuments), "COSMOS", "CSP", "Setup");

        #region structs...
        struct m365BusinessCspInfo
        {
            public string Subscription_OrganizationUnitName;
            public string TenantId;  // Subscription_OMSTenantId;
            public string Offer_Id;
            public string Subscription_Id;
            public string Offer_Name;
            public int    Subscription_IncludedQuantity;
            public string Subscription_StartDate;
            public string Subscription_EndDate;
            public string Tenant_Name;
            public string Subscription_ChannelName;
            public string Subscription_StateKey;
        }

        struct StartedOfficeSetup : IEqualityComparer<StartedOfficeSetup>, IComparable<StartedOfficeSetup>
        {
            public string TenantId;    // OMSTenantId;
            public string Name;
            public string Subscriptions;

            public int  CompareTo(StartedOfficeSetup other)                => this.TenantId.CompareTo(other.TenantId);
            public bool Equals(StartedOfficeSetup x, StartedOfficeSetup y) => x.TenantId.Equals(y.TenantId) && x.Name.Equals(y.Name);
            public int  GetHashCode(StartedOfficeSetup obj)                => obj.GetHashCode();

            public override bool   Equals(object obj) => this.TenantId.Equals(((StartedOfficeSetup)obj).TenantId);
            public override int    GetHashCode()      => TenantId.GetHashCode();
            public override string ToString()         => $"({TenantId} - {Name}";
        }

        struct CompletedOfficeSetup : IEqualityComparer<CompletedOfficeSetup>, IComparable<CompletedOfficeSetup>
        {
            public string TenantId;    // OMSTenantId;
            public string Name;
            public string Subscriptions;

            public int  CompareTo(CompletedOfficeSetup other)                  => this.TenantId.CompareTo(other.TenantId);
            public bool Equals(CompletedOfficeSetup x, CompletedOfficeSetup y) => x.TenantId.Equals(y.TenantId) && x.Name.Equals(y.Name);
            public int  GetHashCode(CompletedOfficeSetup obj)                  => obj.GetHashCode();

            public override bool   Equals(object obj) => this.TenantId.Equals(((CompletedOfficeSetup)obj).TenantId);
            public override int    GetHashCode()      => TenantId.GetHashCode();
            public override string ToString()         => $"({TenantId} - {Name}";
        }

        struct OfficeInstallation
        {
            public string TimeStamp;
            public string TenantId;
            public string RequestPath; // PN
            public string AdHoc0;
            public string AdHoc1;
        }

        struct Results
        {
            public string OrgName;
            public string TenantId;
            public string TenantName;
            public string OfferId;
            public string OfferName;
            public string SubscriptionId;
            public int    Licenses;
            public string State_Key;
            public string StartDate;
            public string EndDate;
            public string ChannelName;
            public string InstallationState; // Setup Wizard
            public string Business;
            public string BusinessPremium;
            public string BusinessEssentials;
            public string EnterpriseE3;
            public string ExchangeOnline;

            // From TenantInfo
            public string TenantCountry;
            public string TenantStartDate;
            public string TotalUsers;
            public string LicensedUsers;
            public string SeatSize_LicensedUsers;
            public string HasEducation;
            public string HasExchange;
            public string HasLync;
            public string HasSharepoint;
            public string HasYammer;
            public string HasProject;
            public string HasPaid;
            public string HasVisio;
            public string IsOnlyInCP;
            public string IsO365;

            public override string ToString() => $"{OrgName},{TenantId},{TenantName},{OfferId},{OfferName},{SubscriptionId},{Licenses},{State_Key},{StartDate},{EndDate},{ChannelName},{InstallationState},{Business},{BusinessPremium},{BusinessEssentials},{EnterpriseE3},{ExchangeOnline}" +
                $"{TenantCountry},{TenantStartDate},{TotalUsers},{LicensedUsers},{SeatSize_LicensedUsers},{HasEducation},{HasExchange},{HasLync},{HasSharepoint},{HasYammer},{HasProject},{HasPaid},{HasVisio},{IsOnlyInCP},{IsO365}";

            public override bool Equals(object obj)
            {
                if (!(obj is Results))
                    return false;

                var other = (Results)obj;

                return (this.OrgName        == other.OrgName)        && (this.TenantId               == other.TenantId)               && (this.TenantName         == other.TenantName)        && (this.OfferId     == other.OfferId)
                    && (this.OfferName      == other.OfferName)      && (this.SubscriptionId         == other.SubscriptionId)         && (this.Licenses           == other.Licenses)          && (this.StartDate   == other.StartDate)
                    && (this.EndDate        == other.EndDate)        && (this.ChannelName            == other.ChannelName)            && (this.InstallationState  == other.InstallationState)
                    && (this.Business       == other.Business)       && (this.BusinessPremium        == other.BusinessPremium)        && (this.BusinessEssentials == other.BusinessEssentials)
                    && (this.EnterpriseE3   == other.EnterpriseE3)   && (this.ExchangeOnline         == other.ExchangeOnline)         && (this.State_Key          == other.State_Key)
                    && (this.TenantCountry  == other.TenantCountry)  && (this.TenantStartDate        == other.TenantStartDate)        && (this.LicensedUsers      == other.LicensedUsers)
                    && (this.TotalUsers     == other.TotalUsers)     && (this.SeatSize_LicensedUsers == other.SeatSize_LicensedUsers) && (this.HasEducation       == other.HasEducation)      && (this.HasExchange == other.HasExchange)
                    && (this.HasLync        == other.HasLync)        && (this.HasSharepoint          == other.HasSharepoint)          && (this.HasYammer          == other.HasYammer)         && (this.HasProject  == other.HasProject)
                    && (this.HasPaid        == other.HasPaid)        && (this.HasVisio               == other.HasVisio)               && (this.IsOnlyInCP         == other.IsOnlyInCP)        && (this.IsO365      == other.IsO365);
            }

            public override int GetHashCode() => this.ToString().GetHashCode();
        }

        struct m365bSignUpAndTenantInfo : IEqualityComparer<m365bSignUpAndTenantInfo>, IComparable<m365bSignUpAndTenantInfo>
        {
            public string OrgUnitName;
            public string TenantID;
            public string OfferID;
            public string SubscriptionID;
            public string OfferName;
            public string Quantity;
            public string StartDate;
            public string EndDate;
            public string TenantName;
            public string ChannelType;
            public string State;
            public string TenantCountry;
            public string OmsTenantId;
            public string TenantStartDate;
            public string TenantState;
            public string TotalUsers;
            public string LicensedUsers;
            public string SeatSize_LicensedUsers;
            public string HasEducation;
            public string HasExchange;
            public string HasLync;
            public string HasSharepoint;
            public string HasYammer;
            public string HasProject;
            public string HasPaid;
            public string HasVisio;
            public string IsOnlyInCP;
            public string IsO365;

            
            public int  CompareTo(m365bSignUpAndTenantInfo other)                      => this.ToString().CompareTo(other.ToString());
            public bool Equals(m365bSignUpAndTenantInfo x, m365bSignUpAndTenantInfo y) => x.ToString().Equals(y.ToString());
            public int  GetHashCode(m365bSignUpAndTenantInfo obj)                      => obj.GetHashCode();

            public override string ToString() => $"{OrgUnitName},{TenantID},{OfferID},{SubscriptionID},{OfferName},{Quantity},{StartDate},{EndDate},{TenantName},{ChannelType},{State},{TenantCountry},{OmsTenantId},{TenantStartDate},{TenantState},{TotalUsers},{LicensedUsers},{SeatSize_LicensedUsers},{HasEducation},{HasExchange},{HasLync},{HasSharepoint},{HasYammer},{HasProject},{HasPaid},{HasVisio},{IsOnlyInCP},{IsO365}";

            public override bool Equals(object obj)
            {
                if (!(obj is m365bSignUpAndTenantInfo))
                    return false;

                var other = (m365bSignUpAndTenantInfo)obj;

                return (this.OrgUnitName    == other.OrgUnitName)    && (this.TenantID               == other.TenantID)               && (this.OfferID         == other.OfferID)         && (this.LicensedUsers == other.LicensedUsers)
                    && (this.SubscriptionID == other.SubscriptionID) && (this.OfferName              == other.OfferName)              && (this.Quantity        == other.Quantity)        && (this.StartDate     == other.StartDate)
                    && (this.EndDate        == other.EndDate)        && (this.TenantName             == other.TenantName)             && (this.ChannelType     == other.ChannelType)     && (this.State         == other.State)
                    && (this.TenantCountry  == other.TenantCountry)  && (this.OmsTenantId            == other.OmsTenantId)            && (this.TenantStartDate == other.TenantStartDate) && (this.TenantState   == other.TenantState)
                    && (this.TotalUsers     == other.TotalUsers)     && (this.SeatSize_LicensedUsers == other.SeatSize_LicensedUsers) && (this.HasEducation    == other.HasEducation)    && (this.HasExchange   == other.HasExchange)
                    && (this.HasLync        == other.HasLync)        && (this.HasSharepoint          == other.HasSharepoint)          && (this.HasYammer       == other.HasYammer)       && (this.HasProject    == other.HasProject)
                    && (this.HasPaid        == other.HasPaid)        && (this.HasVisio               == other.HasVisio)               && (this.IsOnlyInCP      == other.IsOnlyInCP)      && (this.IsO365        == other.IsO365);
            }

            public override int GetHashCode() => this.ToString().GetHashCode();
        }

        #endregion structs...

        const string ColumnHeaderRow           = "OrgUnitName,TenantID,TenantName,OfferId,OfferName,SubscriptionId,Licenses,State,StartDate,EndDate,ChannelName,Setup Wizard,Office365Business,Office365BusinessPremium,Office365BusinessEssentials,EnterpriseE3,ExchangeOnlineEssentials";
        const string ColumnHeaderRowProduction = "ChannelName,Disti,OrgUnitName,TenantID,TenantName,OfferID,OfferName,SubscriptionID,Licenses,State,StartDate,EndDate,SetUpState,O365Business,O365BusinessPremium,O365BusinessEssentials,O365E3,ExchangeOnly,TenantCountry,TenantStartDate,TotalUsers,LicensedUsers,SeatSize_LicensedUsers,HasEducation,HasExchange,HasLync,HasSharepoint,HasYammer,HasProject,HasPaid,HasVisio,IsOnlyInCP,IsO365,ReportDate";

        private static string filenameStartedOfficeSetup = String.Format("StartSetupTenants.Aug02-{0}{1}.csv",
            DateTime.Now.AddDays(daysSetup).ToString("MMM"),
            DateTime.Now.AddDays(daysSetup).Day.ToString());
        
        private static string filenameCompletedOfficeSetup = String.Format("CompleteSetupTenants.Aug02-{0}{1}.csv",
            DateTime.Now.AddDays(daysSetup).ToString("MMM"),
            DateTime.Now.AddDays(daysSetup).Day.ToString());
        
        private static string urlStartedOfficeSetup   = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameStartedOfficeSetup}";
        private static string urlCompletedOfficeSetup = $"https://cosmos14.osdinfra.net/cosmos/office.adhoc/my/csp/{filenameCompletedOfficeSetup}";


        static void Main(string[] args)
        {
            var scopeScriptFilePath  = Path.Combine(CurrentDirectory, "Scope.script");

            //DeleteScopeTempDirectory(); // There's a lock on ScopeEngine.dll in the temp directory by this process...

            RunJobInCOSMOS(scopeScriptFilePath);

            WriteLine($"\n* m365b:\\> Downloading files from COSMOS DB!");

            #region Download the .csv files from COSMOS
            DownloadCSVFile(filenamePreview,              urlPreview);
            DownloadCSVFile(filenameProduction,           urlProduction);
            DownloadCSVFile(filenameBusiness,             urlBusiness);
            DownloadCSVFile(filenameBusinessEss,          urlBusinessEss);
            DownloadCSVFile(filenameBusinessPrem,         urlBusinessPrem);
            DownloadCSVFile(filenameEnterpriseE3,         urlEnterpriseE3);
            DownloadCSVFile(filenameExchange,             urlExhange);
            DownloadCSVFile(filenameCombinedPrev,         urlCombinedPrev);
            DownloadCSVFile(filenameCombinedProd,         urlCombinedProd);
            DownloadCSVFile(filenameStartedOfficeSetup,   urlStartedOfficeSetup,   isSetup: true);
            DownloadCSVFile(filenameCompletedOfficeSetup, urlCompletedOfficeSetup, isSetup: true);
            #endregion Download the .csv files from COSMOS

            WriteLine($"\n* m365b:\\> Downloading files from COSMOS DB finished!");

            UploadSetupCSVToSharePoint(filenameStartedOfficeSetup);
            UploadSetupCSVToSharePoint(filenameCompletedOfficeSetup);

            #region Populate our TenantID List<T> Data Structures..
            business           = GetTenantIDs(Path.Combine(localCSPDirectory, filenameBusiness));
            businessEssentials = GetTenantIDs(Path.Combine(localCSPDirectory, filenameBusinessEss));
            businessPremium    = GetTenantIDs(Path.Combine(localCSPDirectory, filenameBusinessPrem));
            enterpriseE3       = GetTenantIDs(Path.Combine(localCSPDirectory, filenameEnterpriseE3));
            exchangeOnline     = GetTenantIDs(Path.Combine(localCSPDirectory, filenameExchange));
            #endregion Populate our TenantID List<T> Data Structures..

            starteds   = RetreiveStartedOfficeSetups();
            completeds = RetreiveCompletedOfficeSetups();


            WriteLine($"\n* m365b:\\> Processing Pilot/Preview Subscriptions!");

            tenantInfos = Retreivem365bSignUpAndTenantInfos(filenameCombinedPrev);
            ParseCSVFile(filenamePreview, out var rowsPreview);
            RemoveDuplicateRows(rowsPreview, ColumnHeaderRow);
            CreateOutputCSVFile(rowsPreview, ColumnHeaderRowProduction, $"{localCSPDirectory}\\{sharePointFileNamePreview}");
            UploadOutputCSVToSharePoint(filename: sharePointFileNamePreview, isPreview: true);

            WriteLine($"\n* m365b:\\> Finished!");
            WriteLine($"\n* m365b:\\> Processing Production Subscriptions!");

            tenantInfos = Retreivem365bSignUpAndTenantInfos(filenameCombinedProd);
            ParseCSVFile(filenameProduction, out var rowsProduction);
            RemoveDuplicateRows(rowsProduction, ColumnHeaderRow);
            CreateOutputCSVFile(rowsProduction, ColumnHeaderRowProduction, $"{localCSPDirectory}\\{sharePointFileNameProduction}");
            UploadOutputCSVToSharePoint(filename: sharePointFileNameProduction, isPreview: false);


            //DeleteScopeTempDirectory();

            WriteLine($"\n* m365b:\\> Finished!");
            WriteLine($"\n* m365b:\\> Press any key to exit.");
            ReadKey();
        }

        private static void RunJobInCOSMOS(string scopeScriptFilePath)
        {
            // Using FQDNs for learning/memorization purposes...
            var submitParams = new ScopeClient.SubmitParameters(scopeScriptFilePath)
            {
                FriendlyName = "Microsoft 365 Business CSP data v2",
                Parameters   = new Dictionary<string, string>()
                {
                    { "AddDays", days.ToString()},
                    { "AddDaysTenantInfo", daysTenantInfo.ToString()},
                    { "AddDaysSetup", daysSetup.ToString()}
                }
            };

            WriteLine($"* m365b:\\> Submitting COSMOS job: '{submitParams.FriendlyName}'.\n");

            // https://cosmos14.osdinfra.net/
            var jobInfo = ScopeClient.Scope.Submit(vcName: "vc://cosmos14/office.adhoc", proxy: null, defaultCredential: true, parameters: submitParams);

            WriteLine($"\n* m365b:\\> COSMOS job submitted!\n   Job Name: {jobInfo.Name}\n   Job ID:   {jobInfo.ID}\n");

            #region Updating Console Window while Job is running in COSMOS..
            var currentState = VcClient.JobInfo.JobState.Queued;
            Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.");

            do
            {
                if (jobInfo.State != VcClient.JobInfo.JobState.Queued)
                {
                    if (jobInfo.State != currentState)
                    {
                        currentState = jobInfo.State;
                        Write($"\r* m365b:\\> Total Queued Time: {jobInfo.TotalQueuedTime}                   \n\n");
                    }

                    Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.  [Total Running Time: {jobInfo.TotalRunningTime}]");
                }
                Sleep(250);

                // We have to manually refresh the JobInfo object.
                jobInfo = VcClient.VC.GetJobInfo(jobInfo.ID, true);
            }
            while (jobInfo.State == VcClient.JobInfo.JobState.Running || jobInfo.State == VcClient.JobInfo.JobState.Queued);


            WriteLine();
            if (jobInfo.State == VcClient.JobInfo.JobState.Completed || jobInfo.State == VcClient.JobInfo.JobState.CompletedSuccess)
            {
                Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.  [Total Running Time: {jobInfo.TotalRunningTime}]\n");
                WriteLine($"\n* m365b:\\> COSMOS Job completed!");
            }
            else if (jobInfo.State == VcClient.JobInfo.JobState.CompletedFailure)
            {
                Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.  [Total Running Time: {jobInfo.TotalRunningTime}]\n");
                WriteLine($"\n* m365b:\\> COSMOS Job completed but failed!");
            }
            else if (jobInfo.State == VcClient.JobInfo.JobState.Cancelled)
            {
                Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.  [Total Running Time: {jobInfo.TotalRunningTime}]\n");
                WriteLine($"\n* m365b:\\> COSMOS Job cancelled!");
            }
            else if (jobInfo.State == VcClient.JobInfo.JobState.Interrupted)
            {
                Write($"\r* m365b:\\> Current Job State: '{jobInfo.State}'.  [Total Running Time: {jobInfo.TotalRunningTime}]\n");
                WriteLine($"\n* m365b:\\> COSMOS Job interrupted!");
            }
            #endregion

        }

        private static void DownloadCSVFile(string filename, string url, bool isSetup = false)
        {
            var localfile = isSetup ? Path.Combine(localSetupDirectory, filename) : Path.Combine(localCSPDirectory, filename); 

            // Delete the temp file if it already exists..
            if (System.IO.File.Exists(localfile))
            {
                try
                {
                    System.IO.File.Delete(localfile);
                }
                catch
                {
                    // If the file is opened/locked by another process we'll be unable to download the newly created .csv
                    // The file's name is based on yesterday's date, yyyy/mm/dd, and can exist if running this code more than once in a day.
                    WriteLine($"\n* m365b:\\> Unable to delete local file: {localfile}! Is it currently opened by another program?\n");
                    WriteLine("* m365b:\\> Press any key to exit!");
                    Read();
                    Exit(-1);
                }
            }

            WriteLine($"\n* m365b:\\> Downloading file '{filename}' from COSMOS!");


            #region  Downloading file from COSMOS
            // Download the file from COSMOS
            using (var webClient = new WebClient())
            {
                webClient.UseDefaultCredentials = true;
                webClient.DownloadFile(url, localfile);
            }

            WriteLine($"* m365b:\\> Downloading file '{filename}' from COSMOS completed!");
            #endregion
        }

        #region Our List<T> data structures..
        static List<m365BusinessCspInfo>      m365s;
        static List<m365bSignUpAndTenantInfo> tenantInfos;
        static List<StartedOfficeSetup>       starteds;  //   = RetreiveStartedOfficeSetups();
        static List<CompletedOfficeSetup>     completeds;//   = RetreiveCompletedOfficeSetups();
        static List<string> business, businessPremium, businessEssentials, enterpriseE3, exchangeOnline;

        // static List<OfficeInstallation> installions = RetreiveOfficeInstallations();
        #endregion Our List<T> data structures..

        private static void ParseCSVFile(string filename, out List<Results> rows)
        {
            WriteLine($"\n* m365b:\\> Parsing file '{filename}'.");

            var localfile = Path.Combine(localCSPDirectory, filename); //  $"{GetFolderPath(SpecialFolder.MyDocuments)}\\{filename}";

            m365s = Retreivem365BusinessCspInfos(localfile);

            #region LINQ Query
            var query  = from m     in m365s
                         join s     in starteds           on m.TenantId.ToLower() equals s.TenantId.ToLower().Replace("\"", String.Empty) into ms
                         from subS  in ms.DefaultIfEmpty()
                         join c     in completeds         on m.TenantId.ToLower() equals c.TenantId.ToLower().Replace("\"", String.Empty) into cs
                         from subC  in cs.DefaultIfEmpty()
                         join b     in business           on m.TenantId.ToLower() equals b.ToLower()  into mb
                         from subB  in mb.DefaultIfEmpty()
                         join bp    in businessPremium    on m.TenantId.ToLower() equals bp.ToLower() into mbp
                         from subBP in mbp.DefaultIfEmpty()
                         join be    in businessEssentials on m.TenantId.ToLower() equals be.ToLower() into mbe
                         from subBE in mbe.DefaultIfEmpty()
                         join e     in enterpriseE3       on m.TenantId.ToLower() equals e.ToLower()  into me
                         from subE  in me.DefaultIfEmpty()
                         join eo    in exchangeOnline     on m.TenantId.ToLower() equals eo.ToLower() into meo
                         from subEO in meo.DefaultIfEmpty()
                         join ti    in tenantInfos        on m.TenantId.ToLower() equals  ti.TenantID.ToLower() into mti
                         from subTi in mti.DefaultIfEmpty()
                         select new Results {
                                      OrgName                = m.Subscription_OrganizationUnitName,
                                      TenantId               = m.TenantId,
                                      TenantName             = m.Tenant_Name,
                                      OfferId                = m.Offer_Id,
                                      OfferName              = m.Offer_Name,
                                      SubscriptionId         = m.Subscription_Id,
                                      Licenses               = m.Subscription_IncludedQuantity,
                                      State_Key              = m.Subscription_StateKey,
                                      StartDate              = m.Subscription_StartDate,
                                      EndDate                = m.Subscription_EndDate,
                                      ChannelName            = m.Subscription_ChannelName,
                                      InstallationState      = GetSubscriptionState(subC.Subscriptions, subS.Subscriptions),
                                      Business               = !String.IsNullOrEmpty(subB)  ? "Office Business"            : String.Empty,
                                      BusinessPremium        = !String.IsNullOrEmpty(subBP) ? "Office Business Premium"    : String.Empty,
                                      BusinessEssentials     = !String.IsNullOrEmpty(subBE) ? "Office Business Essentials" : String.Empty,
                                      EnterpriseE3           = !String.IsNullOrEmpty(subE)  ? "Enterprise E3"              : String.Empty,
                                      ExchangeOnline         = !String.IsNullOrEmpty(subEO) ? "Exchange Online Essentials" : String.Empty,
                                      TenantCountry          = subTi.TenantCountry,
                                      TenantStartDate        = subTi.TenantStartDate,
                                      TotalUsers             = subTi.TotalUsers,
                                      LicensedUsers          = subTi.LicensedUsers,
                                      SeatSize_LicensedUsers = subTi.SeatSize_LicensedUsers,
                                      HasEducation           = subTi.HasEducation,
                                      HasExchange            = subTi.HasExchange,
                                      HasLync                = subTi.HasLync,
                                      HasSharepoint          = subTi.HasSharepoint,
                                      HasYammer              = subTi.HasYammer,
                                      HasProject             = subTi.HasProject,
                                      HasPaid                = subTi.HasPaid,
                                      HasVisio               = subTi.HasVisio,
                                      IsOnlyInCP             = subTi.IsOnlyInCP,
                                      IsO365                 = subTi.IsO365
                         };
                #endregion    
            

            rows       = query.ToList<Results>();

            WriteLine($"\n* m365b:\\> Finished Parsing file '{filename}'.");

            // C# 7.0 local function
            // https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-7#local-functions
            // C# 6.0 Expression bodied function
            // https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-6#expression-bodied-function-members
            string GetSubscriptionState(string c, string s) => !String.IsNullOrEmpty(c) ? "Completed" : (!String.IsNullOrEmpty(s) ? "Started" : String.Empty);
        }

        private static void RemoveDuplicateRows(List<string> rows, string columnHeaders)
        {
            WriteLine($"\n* m365b:\\> Removing duplicate TenantIDs...");

            // I'm pretty sure there's a better way to do all the below, but
            // I wanted to get it working first, then work on efficiency/elegance/readibility later.
            var dt = new DataTable(tableName: "AllSubsinCSP");
            var cols = dt.Columns;

            // Add Columns to the DataTable
            foreach (var colName in columnHeaders.Split(','))
            {
                dt.Columns.Add(columnName: colName, type: typeof(String));
            }

            // Add Rows from the read-in .csv to DataTable
            foreach (var row in rows)
            {
                var newrow = dt.NewRow();

                string[] cells = row.Split(',');

                for (int index = 0; index < cells.Length && index < dt.Columns.Count; index++)
                {
                    newrow[index] = cells[index];
                }
                dt.Rows.Add(newrow);
            }

            var dupeTenantRowsQuery = from row in dt.Rows.Cast<DataRow>()
                                      group row by (string)row["TenantID"] into g
                                      where g.Count() > 1
                                      select g.Key;

            var dupeTenantRows = dupeTenantRowsQuery.ToList<string>();


            var dupeTenantRowsToKeepQuery = from row in dt.Rows.Cast<DataRow>()
                                            where dupeTenantRows.Contains<string>((string)row["TenantID"])
                                            group row by row["TenantID"] into g
                                            select new { tenantID = g.Key as string, maxDate = g.Max(row => row["StartDate"]) as string };

            var dupeTenantRowsToKeep = dupeTenantRowsToKeepQuery.ToList();

            var dupeTenantRowsToDeleteQuery = from row in dt.Rows.Cast<DataRow>()
                                              where (dupeTenantRows.Contains<string>((string)row["TenantID"])) &&
                                              (!dupeTenantRowsToKeep.Contains(new { tenantID = row["TenantID"] as string, maxDate = row["StartDate"] as string }))
                                              select row;

            var rowsToDelete = dupeTenantRowsToDeleteQuery.ToList<DataRow>();

            // Now that we have the Rows to remove, let's remove them from the List<string> rows

            // If there are no Rows in rowsToDelete, we won't enter inside the foreach below.
            // I'm just ensuring that we use an 'int' for colsCount and not an 'int?' here to avoid any weird .NET
            // issues that may arise from using an 'int?' inside the for-loop inside the following
            // foreach-loop.
            int colsCount = rowsToDelete[0]?.Table.Columns.Count ?? 0;
            int col = 0;
            string temp = default(string);

            // Remove unwanted entries in our List<string> rows (dupe rows)...
            foreach (DataRow row in rowsToDelete)
            {
                temp = default(string);

                for (col = 0; col < colsCount - 1; col++)
                {
                    temp += $"{(string)row[col]},";
                }

                temp += (string)row[col];

                rows.Remove(temp);
            }

            WriteLine($"\n* m365b:\\> {rowsToDelete.Count} Duplicate TenantIDs removed...");
        }
        
        private static void RemoveDuplicateRows(List<Results> rows, string columnHeaders)
        {
            WriteLine($"\n* m365b:\\> Removing duplicate TenantIDs...");

            // I'm pretty sure there's a better way to do all the below, but
            // I wanted to get it working first, then work on efficiency/elegance/readibility later.
            var dt = new DataTable(tableName: "AllSubsinCSP");

            // Add Columns to the DataTable
            foreach (var colName in columnHeaders.Split(','))
            {
                dt.Columns.Add(columnName: colName, type: typeof(String));
            }

            // Add Rows from the read-in .csv to DataTable
            foreach (var row in rows)
            {
                var newrow = dt.NewRow();

                string[] cells = row.ToString().Split(',');

                for (int index = 0; index < cells.Length && index < dt.Columns.Count; index++)
                {
                    newrow[index] = cells[index];
                }
                dt.Rows.Add(newrow);
            }
            List<DataRow> rowsToDelete = new List<DataRow>();

            var dupeTenantRowsQuery = from row in dt.Rows.Cast<DataRow>()
                                      group row by (string)row["TenantID"] into g
                                      where g.Count() > 1
                                      select g.Key;

            var dupeTenantRows = dupeTenantRowsQuery.ToList<string>();

            // If we found no duplicate rows, then we can short circuit out of this method.
            if (dupeTenantRows.Count == 0)
                goto End;

            var dupeTenantRowsToKeepQuery = from row in dt.Rows.Cast<DataRow>()
                                            where dupeTenantRows.Contains<string>((string)row["TenantID"])
                                            group row by row["TenantID"] into g
                                            select new { tenantID = g.Key as string, maxDate = g.Max(row => row["StartDate"]) as string };

            var dupeTenantRowsToKeep = dupeTenantRowsToKeepQuery.ToList();

            var dupeTenantRowsToDeleteQuery = from row in dt.Rows.Cast<DataRow>()
                                              where (dupeTenantRows.Contains<string>((string)row["TenantID"])) &&
                                              (!dupeTenantRowsToKeep.Contains(new { tenantID = row["TenantID"] as string, maxDate = row["StartDate"] as string }))
                                              select row;

            rowsToDelete = dupeTenantRowsToDeleteQuery.ToList<DataRow>();

            // Now that we have the Rows to remove, let's remove them from the List<string> rows

            // If there are no Rows in rowsToDelete, we won't enter inside the foreach below.
            // I'm just ensuring that we use an 'int' for colsCount and not an 'int?' here to avoid any weird .NET
            // issues that may arise from using an 'int?' inside the for-loop inside the following
            // foreach-loop.
            int colsCount = rowsToDelete.Count > 0 ? (rowsToDelete[0]?.Table.Columns.Count ?? 0) : 0;
            var temp = default(Results);

            // Remove unwanted entries in our List<string> rows (dupe rows)...
            foreach (DataRow row in rowsToDelete)
            {
                temp = new Results
                {
                    // "OrgUnitName,TenantID,TenantName,OfferId,OfferName,SubscriptionId,Licenses,StartDate,EndDate,ChannelName,Setup Wizard,Office365Business,Office365BusinessPremium,Office365BusinessEssentials,EnterpriseE3,ExchangeOnlineEssentials";

                    OrgName            = row["OrgUnitName"].ToString(),
                    TenantId           = row["TenantID"].ToString(),
                    TenantName         = row["TenantName"].ToString(),
                    OfferId            = row["OfferId"].ToString(),
                    OfferName          = row["OfferName"].ToString(),
                    SubscriptionId     = row["SubscriptionId"].ToString(),
                    Licenses           = Convert.ToInt32(row["Licenses"].ToString()),
                    StartDate          = row["StartDate"].ToString(),
                    EndDate            = row["EndDate"].ToString(),
                    ChannelName        = row["ChannelName"].ToString(),
                    InstallationState  = row["Setup Wizard"].ToString(), // Setup Wizard
                    Business           = row["Office365Business"].ToString(),
                    BusinessPremium    = row["Office365BusinessPremium"].ToString(),
                    BusinessEssentials = row["Office365BusinessEssentials"].ToString(),
                    EnterpriseE3       = row["EnterpriseE3"].ToString(),
                    ExchangeOnline     = row["ExchangeOnlineEssentials"].ToString()
                };
                

                rows.Remove(temp);
            }

            End:
            WriteLine($"\n* m365b:\\> {rowsToDelete.Count} Duplicate TenantIDs removed...");
        }

        [Obsolete("Don't use! This was for v1 of this EXE.")]
        private static void CreateOutputCSVFile(List<string> rows, string columnHeaders, out string outputFile)
        {
            var output = $"m365BusinessCspInfoSPB-{DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")}-output";
            outputFile = $"{GetFolderPath(SpecialFolder.MyDocuments)}\\M365B-PurchaseSPB.csv";
            outputFile = $"{GetFolderPath(SpecialFolder.MyDocuments)}\\M365B-PurchaseSPB.{DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")}.csv";

            WriteLine($"\n* m365b:\\> Creating formatted file...");
            List<string> rowsOutput = new List<string>();

            if (!outputFile.Contains("InActive"))
            {
                rows.Sort();
            }

            rowsOutput.Add($"Partner,{columnHeaders},ReportDate");

            var now = $"{DateTime.Now.Month}/{DateTime.Now.Day}/{DateTime.Now.Year}";

            // Populate "Special Partners" data structure.
            PopulateSpecialPartners();

            foreach (var row in rows)
            {
                var partner = row.Substring(0, row.IndexOf(','));

                rowsOutput.Add(IsASpecialPartner(partner) ? $"{GetPartnerName(partner)},{row},{now}" : $",{row},{now}");
            }



            goto SkipDynamicNaming;


            #region Dynamic Output File naming
#pragma warning disable CS0162
            outputFile = $"{output}.csv";
#pragma warning restore CS0162
            // Does this file arlready exist?
            if (System.IO.File.Exists(outputFile))
            {
                int copyNumber = 0;
                while (System.IO.File.Exists($"{output}-[{++copyNumber}].csv"))
                {
                    //
                }
                outputFile = $"{output}-[{copyNumber}].csv";
            }
            #endregion


            SkipDynamicNaming:

            string[] outputRows = rowsOutput.ToArray<string>();

            if (System.IO.File.Exists(outputFile))
            {
                try
                {
                    WriteLine($"\n* m365b:\\> Output file '{outputFile}' already exists! Attemping to delete it!");
                    System.IO.File.Delete(outputFile);
                }
                catch
                {
                    WriteLine($"\n* m365b:\\> Unable to delete pre-existing formatted output file '{outputFile}'! Please ensure it's not already opened by another program! Press any key to exit.");
                    Read();
                    Exit(-2);
                }
            }
            WriteLine($"\n* m365b:\\> Successfully deleted previous Output file '{outputFile}'.");

            using (var fs = new FileStream(path: outputFile, mode: FileMode.CreateNew, access: FileAccess.ReadWrite))
            using (var sw = new StreamWriter(stream: fs, encoding: Encoding.UTF8))
            {
                for (int index = 0; index < outputRows.Length; index++)
                {
                    sw.WriteLine(outputRows[index]);
                }
                sw.Flush();
            }

            if (!System.IO.File.Exists(outputFile))
            {
                WriteLine($"\n* m365b:\\> Unable to create formatted output file '{outputFile}'! Press any key to exit.");
                Read();
                Exit(-3);
            }

            WriteLine($"\n* m365b:\\> Formatted File saved locally to '{outputFile}'.");

            // Open the MyDocuments Folder
            //Start(GetFolderPath(SpecialFolder.MyDocuments));
        }

        private static void CreateOutputCSVFile(List<Results> rows, string columnHeaders, string filename)
        {
            WriteLine($"\n* m365b:\\> Creating formatted file...");

            var output = new List<string>();

            // Adding first row: Columns Header Row
            output.Add(columnHeaders);


            var today = $"{DateTime.Now.Month}/{DateTime.Now.Day}/{DateTime.Now.Year}";

            // Populate "Special Partners" data structure.
            PopulateSpecialPartners();

            // Add Rows for each Microsoft 365 Business TenantID..
            foreach (var item in rows)
            {
                var specialPartner = IsASpecialPartner(item.OrgName) ? GetPartnerName(item.OrgName) : String.Empty;

                output.Add($"{item.ChannelName},{specialPartner},{item.OrgName},{item.TenantId},{item.TenantName},{item.OfferId},{item.OfferName}," +
                    $"{item.SubscriptionId},{item.Licenses},{item.State_Key},{item.StartDate},{item.EndDate}," +
                    $"{item.InstallationState},{item.Business},{item.BusinessPremium},{item.BusinessEssentials},{item.EnterpriseE3},{item.ExchangeOnline}," +
                    $"{item.TenantCountry},{item.TenantStartDate},{item.TotalUsers},{item.LicensedUsers},{item.SeatSize_LicensedUsers},{item.HasEducation}," +
                    $"{item.HasExchange},{item.HasLync},{item.HasSharepoint},{item.HasYammer},{item.HasProject},{item.HasPaid},{item.HasVisio}," +
                    $"{item.IsOnlyInCP},{item.IsO365},{today}");
            }

            #region Create/Write to .csv file..
            // Write 'list' out to file...
            using (var fs = new FileStream(path: filename, mode: FileMode.Create, access: FileAccess.ReadWrite))
            using (var sw = new StreamWriter(stream: fs, encoding: Encoding.UTF8))
            {
                for (int index = 0; index < output.Count; index++)
                {
                    sw.WriteLine(output[index]);
                }
                sw.Flush();
            }
            #endregion

        }

        private static void UploadOutputCSVToSharePoint(string filename, bool isPreview)
        {
            WriteLine($"\n* m365b:\\> Uploading file '{filename}' to SharePoint site.");

            using (ClientContext context = new ClientContext("https://microsoft.sharepoint.com/teams/Businesscloudsuite"))
            {
                context.Credentials = GetAADCredsFromConfig();

                string fileUrl = AppSettings.Get("SharePoint_URL").Trim() 
                    + (isPreview ? "Preview/" : "Production/")
                    + filename;

                string localfile = Path.Combine(localCSPDirectory, filename);

                using (FileStream fs = new FileStream(localfile, FileMode.Open))
                {
                    Microsoft.SharePoint.Client.File.SaveBinaryDirect(context, fileUrl, fs, overwriteIfExists: true);
                }
            }
        }

        private static void UploadSetupCSVToSharePoint(string filename)
        {
            WriteLine($"\n* m365b:\\> Uploading file '{filename}' to SharePoint site.");

            using (ClientContext context = new ClientContext("https://microsoft.sharepoint.com/teams/Businesscloudsuite"))
            {
                context.Credentials = GetAADCredsFromConfig();

                string fileUrl = AppSettings.Get("SharePoint_URL").Trim()
                    + ("Setup/")
                    + filename;

                string localfile = Path.Combine(localSetupDirectory, filename);

                using (FileStream fs = new FileStream(localfile, FileMode.Open))
                {
                    Microsoft.SharePoint.Client.File.SaveBinaryDirect(context, fileUrl, fs, overwriteIfExists: true);
                }
            }
        }

        private static SharePointOnlineCredentials GetAADCredsFromConfig()
        {
            // Grab  the Azure Active Domain account and password from app.config.
            var aad = AppSettings.Get("AAD_Account").Trim();
            var pw = AppSettings.Get("AAD_Password").Trim();

            var securedString = new SecureString();

            for (int index = 0; index < pw.Length; index++)
            {
                securedString.AppendChar(pw[index]);
            }

            return new SharePointOnlineCredentials(username: aad, password: securedString);
        }

        private static void DeleteScopeTempDirectory()
        {

            if (Directory.Exists("ScopeWorkingDir"))
            {
                WriteLine($"\n* m365b:\\> Deleting Scope temp directory.");

                try
                {
                    Directory.Delete(path: "ScopeWorkingDir", recursive: true);
                }
                catch
                {
                    WriteLine($"\n* m365b:\\> Failed to delete Scope temp directory!");
                }
            }
        }

        
        #region Helper methods used to populate our List<T> objects..
        static List<m365BusinessCspInfo>  Retreivem365BusinessCspInfos(string filepath)
        {
            var entries = new List<m365BusinessCspInfo>();

            using (var fs = new FileStream(path: filepath, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string columnHeaders = sr.ReadLine();

                    int stateColumnIndex = Array.IndexOf(columnHeaders.Split(','), "State");

                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Unfortunately, since we're consuming a .ss directly and not a view we need
                        // to do some value checks and translations.
                        // eg., State values are represented as integers but represent a Named value.
                        // eg,. In columns where the DateTime should be empty, it's represented as 1/1/1900.
                        line = line.Replace(oldValue: "1/1/1900", newValue: String.Empty);

                        var columns = line.Split(',');
                        columns[stateColumnIndex] = GetState(columns[stateColumnIndex]);
                        //line = String.Join(separator: ",", value: columns);

                        entries.Add(new m365BusinessCspInfo()
                        {
                            Subscription_OrganizationUnitName = columns[0],
                            TenantId                          = columns[1],
                            Offer_Id                          = columns[2],
                            Subscription_Id                   = columns[3],
                            Offer_Name                        = columns[4],
                            Subscription_IncludedQuantity     = Convert.ToInt32(columns[5]),
                            Subscription_StartDate            = columns[6],
                            Subscription_EndDate              = columns[7],
                            Tenant_Name                       = columns[8],
                            Subscription_ChannelName          = columns[9],
                            Subscription_StateKey             = columns[10]
                        });
                    }
                }
            }


            return entries;

            // C# 7.0 local function
            // https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-7#local-functions
            string GetState(string state)
            {
                // C# 7.0 out variable
                // https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-7#out-variables
                var result = Int32.TryParse(state, out var stateId);

                if (!result)
                    return $"Unknown: {state}";

                switch (stateId)
                {
                    case 1:
                        return "Active";
                    case 2:
                        return "InGracePeriod";
                    case 3:
                        return "Disabled";
                    case 4:
                        return "Deprovisioned";
                    default:
                        return $"Unknown: {state}";
                }
            }

        }

        static List<StartedOfficeSetup>   RetreiveStartedOfficeSetups()
        {
            var entries = new List<StartedOfficeSetup>();

            var localfile = Path.Combine(localSetupDirectory, filenameStartedOfficeSetup);

            using (var fs = new FileStream(path: localfile, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var columns = line.Split(',');

                        entries.Add(new StartedOfficeSetup()
                        {
                            TenantId      = columns[0],
                            Name          = columns[1],
                            Subscriptions = columns[2]
                        });
                    }
                }
            }

            //var temp = entries.Select(a => a).Distinct<StartedOfficeSetup>().ToList<StartedOfficeSetup>();

            return entries.Select(a => a).Distinct<StartedOfficeSetup>().ToList<StartedOfficeSetup>();
        }

        static List<CompletedOfficeSetup> RetreiveCompletedOfficeSetups()
        {
            var entries = new List<CompletedOfficeSetup>();

            var localfile = Path.Combine(localSetupDirectory, filenameCompletedOfficeSetup);

            using (var fs = new FileStream(path: localfile, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var columns = line.Split(',');

                        entries.Add(new CompletedOfficeSetup()
                        {
                            TenantId      = columns[0],
                            Name          = columns[1],
                            Subscriptions = columns[2]
                        });
                    }
                }
            }

            //var temp = entries.Select(a => a).Distinct<CompletedOfficeSetup>().ToList<CompletedOfficeSetup>();

            return entries.Select(a => a).Distinct<CompletedOfficeSetup>().ToList<CompletedOfficeSetup>();
        }

        static List<m365bSignUpAndTenantInfo> Retreivem365bSignUpAndTenantInfos(string filename)
        {
            // Columns:
            //
            // OrgUnitName,TenantID,OfferID,SubscriptionID,OfferName,Quantity,StartDate,EndDate,TenantName,ChannelType,State,TenantCountry,OmsTenantId,
            // TenantStartDate,TenantState,TotalUsers,LicensedUsers,SeatSize_LicensedUsers,HasEducation,HasExchange,HasLync,HasSharepoint,
            // HasYammer,HasProject,HasPaid,HasVisio,IsOnlyInCP,IsO365

            var localfile = Path.Combine(localCSPDirectory, filename);

            var entries = new List<m365bSignUpAndTenantInfo>();

            using (var fs = new FileStream(path: localfile, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string line;

                    // The first row/line contains the column headers. Let's skip pass it.
                    sr.ReadLine();

                    while ((line = sr.ReadLine()) != null)
                    {
                        var columns = line.Split(',');

                        entries.Add(new m365bSignUpAndTenantInfo()
                        {
                            OrgUnitName            = columns[0],
                            TenantID               = columns[1],
                            OfferID                = columns[2],
                            SubscriptionID         = columns[3],
                            OfferName              = columns[4],
                            Quantity               = columns[5],
                            StartDate              = columns[6],
                            EndDate                = columns[7],
                            TenantName             = columns[8],
                            ChannelType            = columns[9],
                            State                  = columns[10],
                            TenantCountry          = columns[11],
                            OmsTenantId            = columns[12],
                            TenantStartDate        = columns[13],
                            TenantState            = columns[14],
                            TotalUsers             = columns[15],
                            LicensedUsers          = columns[16],
                            SeatSize_LicensedUsers = columns[17],
                            HasEducation           = columns[18],
                            HasExchange            = columns[19],
                            HasLync                = columns[20],
                            HasSharepoint          = columns[21],
                            HasYammer              = columns[22],
                            HasProject             = columns[23],
                            HasPaid                = columns[24],
                            HasVisio               = columns[25],
                            IsOnlyInCP             = columns[26],
                            IsO365                 = columns[27]
                        });
                    }
                }
            }

            //var temp = entries.Select(a => a).Distinct<CompletedOfficeSetup>().ToList<CompletedOfficeSetup>();

            return entries.Select(a => a).Distinct<m365bSignUpAndTenantInfo>().ToList<m365bSignUpAndTenantInfo>();
        }

        /*
        static List<OfficeInstallation>   RetreiveOfficeInstallations()
        {
            var entries = new List<OfficeInstallation>();

            using (var fs = new FileStream(path: OfficeInstallationFilePath, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var columns = line.Split(',');

                        entries.Add(new OfficeInstallation()
                        {
                            TimeStamp     = columns[0],
                            TenantId      = columns[1],
                            RequestPath   = columns[2],
                            AdHoc0        = columns[3],
                            AdHoc1        = columns[4]
                        });
                    }
                }
            }

            return entries;
        }
        */

        static List<string> GetTenantIDs(string filepath)
        {
            var entries = new List<string>();

            using (var fs = new FileStream(path: filepath, mode: FileMode.Open))
            using (var sr = new StreamReader(stream: fs, encoding: Encoding.UTF8))
            {
                if (fs.CanRead)
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        entries.Add(line);
                    }
                }
            }

            return entries;
        }
        #endregion





        #region Partners stuff..
        static string[] specialPartners;

        private static void PopulateSpecialPartners()
        {
            var partners = AppSettings.Get("Special_Partners").Split(';');

            // Guard against empty entries, leading/trailing white space, and rogue/extra semicolons in the .config file..
            var query = from partner in partners
                        where !String.IsNullOrWhiteSpace(partner)
                        select partner.Trim();

            specialPartners = query.ToArray<string>();
        }


        private static bool IsASpecialPartner(string partner)
        {
            var partnerQuery = from p in specialPartners
                               where partner.ToUpper().Contains(p)
                               select p;

            return partnerQuery.Count<string>() != 0;
        }

        private static string GetPartnerName(string partner)
        {
            var partnerQuery = from p in specialPartners
                               where partner.ToUpper().Contains(p)
                               select p;

            return partnerQuery.First<string>();
        }
        #endregion Partners stuff..

    } // End of class


    internal static class ArrayExtensions
    {
        public static SecureString ToSecureString(this char[] array)
        {
            var securedString = new SecureString();
            for (int index = 0; index < array.Length; index++)
            {
                securedString.AppendChar(array[index]);
            }
            return securedString;
        }

        public static char[] ToCharArray(this string array, int seed)
        {
            return (from ch in array.Split(',')
                    let ch2 = Convert.ToInt32(ch) / seed
                    select ch2).ToArray<int>().Select(i => (char)i).ToArray<char>();
        }
    }
}
