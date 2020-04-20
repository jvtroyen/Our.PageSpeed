using System.Web.Mvc;

namespace Our.PageSpeed.ActionFilters
{
    public interface IKeyGenerator
    {
        string GenerateKey(ControllerContext context);
    }
}