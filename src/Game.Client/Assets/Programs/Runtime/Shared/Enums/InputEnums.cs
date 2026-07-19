namespace Game.Shared.Enums
{
    public enum InputDeviceType
    {
        None = 0,
        Keyboard = 1,
        Mouse = 2,
        PlayStation = 100,
        NintendoSwitch = 200,
        Xbox = 300,
    }

    public static class InputDeviceTypeExtensions
    {
        public static string ToIdentifier(this InputDeviceType deviceType)
        {
            switch (deviceType)
            {
                case InputDeviceType.Keyboard:
                    return "keyboard";
                case InputDeviceType.Mouse:
                    return "mouse";
                case InputDeviceType.PlayStation:
                    return "ps";
                case InputDeviceType.NintendoSwitch:
                    return "switch";
                case InputDeviceType.Xbox:
                    return "xbox";
                default:
                    return string.Empty;
            }
        }
    }
}
