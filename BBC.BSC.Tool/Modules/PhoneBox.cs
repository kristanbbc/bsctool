namespace BBC.BSC.Tool.Modules
{
    public class PhoneBox
    {

        public static PhoneBoxConfig GetPhoneBoxConfig(string region)
        {

            var phoneBoxConfig = new PhoneBoxConfig();
            // TODO make configuration
            switch (region)
            {
                case "West":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBW01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBW02";
                    phoneBoxConfig.OasisAddress = "BBCE-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-OASIS-RESERVE";
                    break;

                case "South":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBS01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBS02";
                    phoneBoxConfig.OasisAddress = "BBCE-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-OASIS-RESERVE";
                    break;

                case "North":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBN01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBN02";
                    phoneBoxConfig.OasisAddress = "BBCE-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-OASIS-RESERVE";
                    break;

                case "Midlands":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBM01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBM02";
                    phoneBoxConfig.OasisAddress = "BBCE-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-OASIS-RESERVE";
                    break;

                case "East":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBE01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBE02";
                    phoneBoxConfig.OasisAddress = "BBCE-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-OASIS-RESERVE";
                    break;

                case "LRTS Station S":
                    phoneBoxConfig.ServerAddress = "3GBV5APPBX6S01"; 
                    phoneBoxConfig.ServerBackupAddress = "3GBV6APPBX6S02";
                    phoneBoxConfig.OasisAddress = "BBCE-LRTS-S-OASIS-MAIN";
                    phoneBoxConfig.OasisBackupAddress = "BBCE-LRTS-S-OASIS-RESERVE";
                    break;

                case "LRTS Station T":
                    phoneBoxConfig.ServerAddress = "3GBV5APPBX6T01"; 
                    phoneBoxConfig.ServerBackupAddress = "3GBV6APPBX6T02";
                    phoneBoxConfig.OasisAddress = "BBCE-LRTS-T-OASIS-MAIN"; 
                    phoneBoxConfig.OasisBackupAddress = "BBCE-LRTS-T-OASIS-RESERVE";
                    break;

                default:
                    //logger.Error("Unknonw phonebox site given");
                    phoneBoxConfig = null;
                    break;
            }

            return phoneBoxConfig;

        }

    }
}
