using System.Web.Mvc;

namespace Our.Umbraco.PageSpeed.ActionFilters
{
    public interface IKeyGenerator
    {
        string GenerateKey(ControllerContext context);
    }
}