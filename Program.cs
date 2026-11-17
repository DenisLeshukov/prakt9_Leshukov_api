using System.Net.Http.Headers;
using System.Net.Http.Json;

public class Program
{
    private static async Task Main(string[] args)
    {
        var client = new HttpClient();

        var response = await client.PostAsJsonAsync(
            "https://localhost:7156/api/v1/auth/login",
            new
            {
                Login = "string",
                Password = "string"
            });
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Ошибка аутентификации");
            return;
            
        }
        var result = await response.Content
                .ReadFromJsonAsync<AuthResponse>();
        string token = result!.access_token;
        string rt = result!.refresh_token;
        Console.WriteLine(rt);


        Thread.Sleep(5000);


        //var response_refresh = await client.PostAsJsonAsync(
        //    "https://localhost:7156/api/v1/auth/refresh",
        //    new
        //    {
        //        token = rt
        //    });
        //if (!response_refresh.IsSuccessStatusCode)
        //{
        //    Console.WriteLine("Ошибка аутентификации");

        //    return;

        //}
        //var result_ref = await response_refresh.Content
        //        .ReadFromJsonAsync<AuthResponse>();
        //string new_refresh_token = result_ref.refresh_token;
        //Console.WriteLine(new_refresh_token);

        client.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("Bearer", token);
        var response_log = await client.PostAsJsonAsync(
            "https://localhost:7156/api/v1/auth/logout",
            new
            {
                token = rt
            });
        if (!response_log.IsSuccessStatusCode)
        {
            Console.WriteLine("Ошибка аутентификации");

            return;

        }


        Console.WriteLine(response_log.StatusCode);
    }
    
    public record AuthResponse(string access_token, string refresh_token);
    
}