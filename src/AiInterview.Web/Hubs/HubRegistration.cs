namespace AiInterview.Web.Hubs;

/// <summary>
/// Nơi map các SignalR hub. Mỗi hub thêm một dòng ở đây khi cài đặt:
/// TODO ING-08 (Dev A): endpoints.MapHub&lt;DocumentHub&gt;("/hubs/documents");
/// </summary>
public static class HubRegistration
{
    public static IEndpointRouteBuilder MapHubs(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
