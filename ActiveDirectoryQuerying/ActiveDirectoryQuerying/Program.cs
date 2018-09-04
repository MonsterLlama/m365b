using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Collections.Generic;
using System.Linq;

using static System.Console;


namespace ActiveDirectoryQuerying
{
    class Program
    {
        const int VENDOR_EMPLOYEE = 92;
        const string NO_OFFICE = "No WorkSpace";
        static bool VERBOSE = false;
        static SortedDictionary<string, string> companyMappings = new SortedDictionary<string, string>();
        static List<string> redmondBuildings = null;
        static List<string> skippedCompanies = null;

        static void Main(string[] args)
        {
            // Verbose logging means we'll display info for each individual AD account.
            if (args.Length > 0 && args[0].ToUpper() == "-V")
                VERBOSE = true;


            //string path = "GC://" + System.Environment.GetEnvironmentVariable("USERDNSDOMAIN");

            using (var searcher = new DirectorySearcher(null, String.Empty))
            {
                //searcher.SearchRoot = new DirectoryEntry(path);
                //var searchResult = searcher.FindOne();

                searcher.PageSize = 500;

                searcher.Filter = $"(extensionAttribute2={VENDOR_EMPLOYEE})";

                searcher.SearchRoot = new DirectoryEntry("GC://OU=UserAccounts,DC=redmond,DC=corp,DC=microsoft,DC=com");

                var searchResult = searcher.FindAll();

                var coll = searchResult;

                int entries = 0;

                var companiesDictionary = new SortedDictionary<string, int>();
                var vendorsPerBuildingDictionary = new SortedDictionary<string, int>();

                PopulateCompanyMappings();
                PopulateSkippedCompanies();
                PopulateRedmondLocations();

                foreach (var entry in coll)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        if (entries == 100)
                            break;
                    }

                    var val = ((SearchResult)entry)?.Properties as ResultPropertyCollection;

                    // If for whatever reason we get an empty record let's just skip it and move on to the next one.
                    if (val?.Count == 0)
                    {
                        continue;
                    }

                    // These are the fields we're currently printing out to the console when running in verbose mode: ActiveDirectoryQuerying.exe -v
                    var cn                       = val?["cn"].Count > 0                         ? val?["cn"]?[0]?.ToString()                         : "empty";
                    var company                  = val?["company"].Count > 0                    ? val?["company"]?[0]?.ToString()                    : "empty";
                    var department               = val?["department"].Count > 0                 ? val?["department"]?[0]?.ToString()                 : "empty";
                    var displayName              = val?["displayName"].Count > 0                ? val?["displayName"]?[0]?.ToString()                : "empty";
                    var msExchWhenMailboxCreated = val?["msExchWhenMailboxCreated"].Count > 0   ? val?["msExchWhenMailboxCreated"]?[0]?.ToString()   : "empty";
                    var name                     = val?["name"].Count > 0                       ? val?["name"]?[0]?.ToString()                       : "empty";
                    var sAMAccountName           = val?["sAMAccountName"].Count > 0             ? val?["sAMAccountName"]?[0]?.ToString()             : "empty";
                    var office                   = val?["physicaldeliveryofficename"].Count > 0 ? val?["physicaldeliveryofficename"]?[0]?.ToString() : "empty";

                    // Let's not include any "business guests": b-
                    if (sAMAccountName.StartsWith("b-", StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Parse the 'office' string to attain the building..
                    // Then update our dictionary tracking vendors per building..
                    // [Assumption]: No building names, themselves, will have the '/' character in them..
                    if (!String.Equals(office, NO_OFFICE, StringComparison.CurrentCultureIgnoreCase) && // Filter out "No WorkSpace" Offices
                        !String.IsNullOrWhiteSpace(office) &&
                        office.Contains("/") &&
                        !skippedCompanies.Contains(company)) // Filter out known, non-tech companies: e.g., landscaping and food service vendor companies..
                    {

                        int separator = office.IndexOf('/');
                        string building = office.Substring(0, separator);

                        if (redmondBuildings.Contains(building))
                        {
                            // Add to our companies Dictionary
                            if (!String.IsNullOrWhiteSpace(company))
                            {
                                // Some companies are listed with different spellings and casing
                                // so we'll check for any known variations and point them to the 
                                // correct, expected spelling & case.
                                if (companyMappings.ContainsKey(company))
                                {
                                    company = companyMappings[company];
                                }

                                if (companiesDictionary.ContainsKey(company))
                                {
                                    companiesDictionary[company]++;
                                }
                                else
                                {
                                    companiesDictionary[company] = 1;
                                }
                            }

                            if (vendorsPerBuildingDictionary.ContainsKey(building))
                            {
                                vendorsPerBuildingDictionary[building]++;
                            }
                            else
                            {
                                vendorsPerBuildingDictionary[building] = 1;
                            }

                            // Print Vendor stats/info...
                            if (VERBOSE)
                            {
                                PrintVendor(cn, company, department, displayName, msExchWhenMailboxCreated, name, sAMAccountName, office);
                            }

                            ++entries;
                        }
                    }
                }


                // Sort the Dictionaries
                /* System.Linq.IOrderedEnumerable<KeyValuePair<string,int>> */
                var vendorQuery = from vendor in vendorsPerBuildingDictionary
                                  orderby vendor.Value descending
                                  select vendor;

                var companyQuery = from company in companiesDictionary
                                   orderby company.Value descending
                                   select company;

                #region output to console
                WriteLine();
                WriteLine("-------------------------------------------------------------------------------");
                WriteLine($"Entries: {entries}");
                WriteLine("-------------------------------------------------------------------------------");
                WriteLine();
                WriteLine($"Buildings: {vendorsPerBuildingDictionary?.Count}");
                WriteLine("-------------------------------------------------------------------------------");
                WriteLine("Vendor per Building and Head Count in Redmond");
                WriteLine("-------------------------------------------------------------------------------");
                foreach (var building in vendorQuery)
                {
                    WriteLine($"{building.Value}\t\t {building.Key}");
                }

                WriteLine();
                WriteLine("-------------------------------------------------------------------------------");
                WriteLine($"Companies: {companiesDictionary?.Count}");
                WriteLine("-------------------------------------------------------------------------------");
                WriteLine("Company and Head Count in Redmond");
                WriteLine("-------------------------------------------------------------------------------");
                foreach (var company in companyQuery)
                {
                    WriteLine($"{company.Value}\t\t {company.Key}");
                }
                #endregion

            }
        }

        private static List<string> PopulateRedmondLocations()
        {
            redmondBuildings = new List<string>(BUILDINGS);

            return redmondBuildings;
        }

        private static void PrintVendor(string cn, string company, string department, string displayName, string msExchWhenMailboxCreated, string name, string sAMAccountName, string office)
        {

            WriteLine($"cn\t\t\t = {cn}");
            WriteLine($"company\t\t\t = {company}");
            WriteLine($"department\t\t = {department}");
            WriteLine($"displayName\t\t = {displayName}");
            WriteLine($"msExchWhenMailboxCreated = {msExchWhenMailboxCreated}");
            WriteLine($"name\t\t\t = {name}");
            WriteLine($"sAMAccountName\t\t = {sAMAccountName}");
            WriteLine($"office\t\t\t = {office}");
            WriteLine("-------------------------------------------------------------------------------");
            WriteLine();
        }

        #region Arrays for buildings in Redmond & skipped companies...
        // You gotta love this technique...
        private static string[] BUILDINGS = {"1", "10", "109", "11", "111", "112", "113", "114", "115", "120", "121", "122", "123", "124", "125", "126",
            "127", "16", "17", "18", "19", "2", "20", "21", "22", "24", "25", "26", "27", "28", "3", "30", "31", "32", "33", "34", "35", "36", "37", "4", "40",
            "41", "42", "43", "44", "47", "5", "50", "6", "8", "83", "84", "85", "86", "87", "88", "9", "92", "99", "ADVANTA-A", "ADVANTA-B", "ADVANTA-C",
            "BELLEVUE-1814", "BRAVERN-1", "BRAVERN-2", "CITY CENTER", "KIRKLAND-434", "KIRKLAND-550", "LINCOLN SQUARE", "MARYMOOR", "MILLENNIUM A",
            "MILLENNIUM B", "MILLENNIUM C", "MILLENNIUM D", "MILLENNIUM E", "MILLENNIUM F", "REDMOND RIDGE", "1", "REDMOND TOWN", "B4", "REDMOND TOWN", "B5",
            "REDMOND TOWN B6", "REDMOND WOODS-A", "REDMOND WOODS-C", "REDMOND-17760", "REDW-A", "REDW-B", "REDW-C", "REDW-D", "REDW-E", "REDW-FSAMM-C",
            "SAMM-D", "SEA-320WESTLAKE", "STUDIO A", "STUDIO B", "STUDIO C", "STUDIO D", "STUDIO E", "STUDIO F", "STUDIO G", "STUDIO H", "STUDIO X",
            "TUKWILA-5", "WILLOWS WHSE", "WILLOWS-10525", "WILLOWS-9825", "WILLOWS-9911"};

        // These are known, non-tech companies: e.g., landscaping, physical security, dining, legal, driving, etc..
        private static string[] SKIPPED_COMPANIES = { "CBRE", "CBRE, Inc", "Novitex", "Securitas Security Services US", "Covestic Inc", "Compass Group NAD- Event Mgmt",
        "Novitex Enterprise Solutions", "Compass Group USA", "COMPASS GROUP NAD-EVENT MANAGE", "Securitas Security Services", "Eurest Dining Services", "COMPASS GROUP USA",
        "ABLE Building Maintenance", "ABLE Services", "Carpool Agency Inc", "Casaba Security LLC", "Cochran Electric", "Cochran Inc", "Compass Group",
        "Compass Partners LLC", "Compass USA", "Dell", "Dell Computers", "Dell Marketing LP", "Deloitte & Touche", "eCompanyStore", "Ernst & Young",
        "ERNST AND YOUNG LLP", "Frontier", "Frontier Communications Northw", "GRANT THORNTON LLP", "Grant Thornton", "Hewlett Packard", "Hewlett Packard - Bellevue",
        "Hewlett Packard Company", "Hewlett Packard Enterprise", "Hewlett-Packard", "MV Public Transportation Inc", "MV Transportation Inc",
        "PRICEWATERHOUSECOOPERS LLP", "Pricewaterhousecoopers Outsour", "Pricewaterhouse Coopers Privat", "PRICE WATERHOUSE COOPERS LLP", "Price Waterhouse Coopers",
        "SECURITAS SECURITY SERVICES LT", "Unisys", "Unisys  Corp", "Unisys Corp", "Unisys Corporation", "UNISYS CORPORATION", "Able Building Maintenance",
        "Able Building Services", "American Business Consulting", "American Express Travel"};
        #endregion

        private static void PopulateSkippedCompanies()
        {
            skippedCompanies = new List<string>(SKIPPED_COMPANIES);
        }

        private static void PopulateCompanyMappings()
        {
            companyMappings = new SortedDictionary<string, string>
            {
                ["110 Consulting"] = "110 Consulting Inc",
                ["Accenture LLP"] = "Accenture",
                ["Accountemps"] = "AccounTemps",
                ["Adaquest Inc."] = "adaQuest Inc",
                ["Adaquest, Inc."] = "adaQuest Inc",
                ["Aditi Staffing"] = "Aditi Staffing LLC",
                ["Aditi Technologies Private LTD"] = "Aditi Staffing LLC",
                ["Aerotek"] = "Aerotek US",
                ["Affirma Consulting"] = "Affirma Consulting LLC",
                ["AIM CONSULTING GROUP WASHINGTO"] = "Aim Consulting",
                ["AIM CONSULTING GROUP"] = "Aim Consulting",
                ["Aim Consulting Group WA"] = "Aim Consulting",
                ["Akvelon INC"] = "Akvelon",
                ["Allegis Group Holdings Inc"] = "Allegis Group Services Inc",
                ["Allovus"] = "Allovus Design Inc",
                ["Annik Inc"] = "Annik Technology Services",
                ["AON Risk Services"] = "Aon Risk Services",
                ["APEX SYSTEMS INC"] = "Apex Systems Inc",
                ["Aqauent"] = "Aquent LLC",
                ["Aquent"] = "Aquent LLC",
                ["AS Solution"] = "AS Solution North America Inc",
                ["As Solutions"] = "AS Solution North America Inc",
                ["AS Solutions NA Inc"] = "AS Solution North America Inc",
                ["Atos"] = "Atos IT Solutions and Services",
                ["AtoS"] = "Atos IT Solutions and Services",
                ["Atos It Solutions And Services"] = "Atos IT Solutions and Services",
                ["Avanade"] = "Avanade Inc",
                ["AVANADE INC"] = "Avanade Inc",
                ["BDO"] = "BDO USA LLP",
                ["Beyondsoft"] = "Beyondsoft Consulting Inc",
                ["Beyondsoft Corporation"] = "Beyondsoft Consulting Inc",
                ["Bloom Consulting & Project"] = "Bloom Consulting Group Inc",
                ["Bloom Consulting Group INC"] = "Bloom Consulting Group Inc",
                ["BluLink Solutions"] = "Blulink Solutions LLC",
                ["Boston Consulting Group"] = "Boston Consulting Group Inc",
                ["Bridge Consulting Group LLC"] = "Bridge Partners",
                ["BRILLIO LLC"] = "Brillio LLC",
                ["C2S Technologies"] = "C2S Technologies Inc",
                ["C2S Technologies India Pvt. Lt"] = "C2S Technologies Inc",
                ["Cadence Preferred"] = "Cadence 3 LLC",
                ["CASCADE ENGINEERING SERVICES I"] = "Cascade Engineering Svcs Inc",
                ["CENTRIC CONSULTING LLC"] = "Centric Consulting LLC",
                ["Clearplan LLC"] = "ClearPlan LLC",
                ["Cognizant"] = "Cognizant Technology Solutions",
                ["CSG Services Corp"] = "CSG Services Corporation",
                ["DATA GLOVE INCORPORATED DBA TR"] = "Data Glove Inc",
                ["DATA GLOVE INC"] = "Data Glove Inc",
                ["Denali Advanced Integration"] = "Denali Consulting",
                ["Design Laboratory"] = "Design Laboratory Inc",
                ["Direct Apps Inc"] = "Direct Apps Inc.",
                ["EASI LLC"] = "Easi LLC",
                ["eXcell"] = "CompuCom Systems Inc",
                ["eXcell, a division of CompuCom"] = "CompuCom Systems Inc",
                ["FILTER"] = "Filter LLC",
                ["FiveBy"] = "Fiveby Solutions Inc",
                ["GP Strategies"] = "GP Strategies Corporation",
                ["GP STRATEGIES NETHERLANDS B V"] = "GP Strategies Corporation",
                ["HCL Technologies Ltd"] = "HCL America Inc",
                ["Hitachi Consulting"] = "Hitachi Consulting Corporation",
                ["I2E LLC"] = "i2e LLC",
                ["InConsulting"] = "InConsulting Inc.",
                ["Infosys BPO Limited"] = "Infosys Ltd",
                ["Infosys"] = "Infosys Ltd",
                ["Insight Global Inc"] = "Insight Global",
                ["Insight Direct USA Inc"] = "Insight Global",
                ["Insight Systems Inc"] = "Insight Global",
                ["Inviso"] = "Inviso Corporation",
                ["Iron Mountain"] = "Iron Mountain Information Mgmt",
                ["iSoftStone"] = "iSoftStone Inc",
                ["iSoftStone Inc"] = "iSoftStone Inc",
                ["ISoftStone Information Technol"] = "iSoftStone Inc",
                ["JEFFREYM CONSULTING"] = "JeffreyM Consulting LLC",
                ["Kalia"] = "Kalvi Consulting Services Inc",
                ["Keywords International Inc"] = "Keystone Strategy LLC",
                ["Keywords International Limited"] = "Keywords International Limited",
                ["KFORCE INC"] = "Kforce",
                ["KPMG LLP"] = "KPMG LLP Seattle",
                ["Kwest Engineering"] = "KWest Engineering",
                ["LATENTVIEW ANALYTICS CORPORATI"] = "Latentview Analytics Corp",
                ["LIONBRIDGE TECHNOLOGIES"] = "Lionbridge Technologies Inc",
                ["Lockheed Martin"] = "Lockheed Martin Services",
                ["Loft9 Business Services"] = "Loft 9 LLC",
                ["LOFT9 Business Services"] = "Loft 9 LLC",
                ["Logic20/20"] = "LOGIC20/20 INC",
                ["Luxoft USA Inc"] = "Luxoft",
                ["MAQ LLC"] = "Maq LLC",
                ["MAQ Software"] = "Maq LLC",
                ["Marketscape Inc"] = "Marketscape Inc",
                ["Matisia Inc"] = "Matisia Consultants",
                ["McKinsey"] = "McKinsey",
                ["McKinstry Co."] = "McKinsey",
                ["Mckinstry Essention LLC"] = "McKinsey",
                ["MCKinstry Essention LLC"] = "McKinsey",
                ["Microland LTD"] = "Microland Limited",
                ["Mindtree"] = "Mindtree LTD",
                ["Mindtree Consulting PVT LTD"] = "Mindtree LTD",
                ["MINDTREE LIMITED"] = "Mindtree LTD",
                ["Mod Squad INC"] = "Mod Squad Inc",
                ["Mod Squad Inc"] = "Mod Squad Inc",
                ["Moravia IT AS (USD)"] = "Moravia IT",
                ["MOTIV INC"] = "Motiv Inc",
                ["Nayamode Inc Redmond Office"] = "Nayamode Inc",
                ["NCS"] = "NCC Group Security Services Lt",
                ["Novitex Enterprise Solutions"] = "Novitex",
                ["Novitex Enterprise Solutions"] = "Novitex",
                ["Omega General Contractors"] = "Omega",
                ["Omega Industrial Contractors"] = "Omega",
                ["Pactera Technologies"] = "Pactera Technologies Inc",
                ["PACTERA TECHNOLOGIES INC"] = "Pactera Technologies Inc",
                ["Pactera Technology Limited"] = "Pactera Technologies Inc",
                ["Persistent"] = "Persistent Systems Ltd.",
                ["Pinkerton Consulting And"] = "Pinkerton Inc",
                ["Point B"] = "Point B Inc",
                ["Populus Group"] = "Populus Group LLC",
                ["Populus Group, LLC"] = "Populus Group LLC",
                ["Possible WorldWide Inc"] = "Possible Worldwide Inc",
                ["Prime 8 Consulting"] = "Prime 8",
                ["Prime 8 LLC"] = "Prime 8",
                ["Rampgroup"] = "Ramp Technology Group LLC",
                ["Randstad North America LP"] = "Randstad",
                ["Randstad Sourceright"] = "Randstad",
                ["RedCloud"] = "RedCloud Consulting Inc",
                ["Redcloud INC"] = "RedCloud Consulting Inc",
                ["Resources Connection"] = "Resources Connection Inc",
                ["Resources Global Professionals"] = "Resources Connection Inc",
                ["Resources Online"] = "Resources Connection Inc",
                ["Revel Consulting"] = "Revel Inc",
                ["Robert Half"] = "Robert Half International",
                ["Robert Half Technology"] = "Robert Half International",
                ["RS SOLUTIONS LLC"] = "RS SOLUTIONS LLC",
                ["Rylem"] = "Rylem LLC",
                ["Siemens"] = "Siemens IT Solutions",
                ["Siemens Business Services Inc"] = "Siemens IT Solutions",
                ["Siemens Product Lifecycle MNGT"] = "Siemens IT Solutions",
                ["Simple Concepts"] = "Simple Concepts Consulting LLC",
                ["Slalom Consulting LLC"] = "Slalom Consulting LLC",
                ["Slalom LLC"] = "Slalom Consulting LLC",
                ["Sogeti"] = "Sogeti USA",
                ["Sogeti USA"] = "Sonata Software North America",
                ["Strategic Business Decisions"] = "Strategic Business Decisions",
                ["Swirl Inc."] = "Swift Group Inc",
                ["Swift Group Inc"] = "Synaxis Corporation",
                ["Tata Consultancy Services Ltd"] = "Tata Consultancy Services",
                ["TCG"] = "TCG Advisors LLC",
                ["TECH MAHINDRA LTD."] = "Tech Mahindra Ltd",
                ["Teknon"] = "Teknon Corporation",
                ["TEKsystems Inc"] = "TEKsystems",
                ["Teksystems"] = "TEKsystems",
                ["UNIFYCLOUD LLC"] = "Unifycloud LLC",

            };

        }


    }
}
