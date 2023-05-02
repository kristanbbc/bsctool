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
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "South":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBS01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBS02";
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "North":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBN01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBN02";
                    phoneBoxConfig.OasisAddress = "3GBV1APOAS1001";
                    phoneBoxConfig.OasisBackupAddress = "3GBV2APOAS1001";
                    break;

                case "Midlands":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBM01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBM02";
                    phoneBoxConfig.OasisAddress = "3GBV1APOAS1001";
                    phoneBoxConfig.OasisBackupAddress = "3GBV2APOAS1001";
                    break;

                case "East":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBE01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBE02";
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "LRTS Station S":
                    phoneBoxConfig.ServerAddress = "3GBV5APPBX6S01"; 
                    phoneBoxConfig.ServerBackupAddress = "3GBV6APPBX6S02";
                    phoneBoxConfig.OasisAddress = "3GBV5APOAS6S01";
                    phoneBoxConfig.OasisBackupAddress = "3GBV6APOAS6S02";
                    break;

                case "LRTS Station T":
                    phoneBoxConfig.ServerAddress = "3GBV5APPBX6T01"; 
                    phoneBoxConfig.ServerBackupAddress = "3GBV6APPBX6T02";
                    phoneBoxConfig.OasisAddress = "3GBV5APOAS6T11"; 
                    phoneBoxConfig.OasisBackupAddress = "3GBV6APOAS6T12";
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
