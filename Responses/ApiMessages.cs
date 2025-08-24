using Microsoft.Extensions.Localization;


namespace BasarSoft.Responses
{
    public static class ApiMessages
    {
        private static IStringLocalizer<SharedResources>? _localizer;

        public static void Configure(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        public static string Get(string key)
        {
            if (_localizer == null)
                return key; // fallback

            var loc = _localizer[key];
            return loc.ResourceNotFound ? key : loc.Value;
        }
    }
}
