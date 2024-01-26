using System.Net;
using AzureFunctionExample.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Linq;
using Azure.Data.Tables;

namespace AzureFunctionExample;
public class Games
{
    private readonly ILogger _logger;
    private readonly TableClient _table;

    public Games(ILoggerFactory loggerFactory, TableServiceClient tableService)
    {
        // name of the azure storage account table where to create, store, lookup and delete games
        string tableName = "Whishlist";

        _logger = loggerFactory.CreateLogger<Games>();
        // create TableClient for table with name tableName and create table if not exists already
        tableService.CreateTableIfNotExists(tableName);
        _table = tableService.GetTableClient(tableName);
    }

    // get list of all games in wishlist
    // define open api attributes / decorators
    [OpenApiOperation(operationId: "listWishlist", tags: new[] {"wishlist"}, Summary = "Get wishlist", Description = "Get list of games in wishlist.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IList<WishlistModel>))]
    [Function("HTTPListWishlist")]
    public async Task<HttpResponseData> ListGames([HttpTrigger(AuthorizationLevel.Function, "get", Route = "games")] HttpRequestData request)
    {
        _logger.LogInformation($"[{request.FunctionContext.InvocationId}] Processing request for list games endpoint.");

        // get all rows from table storage as list of WishlistTableModel (this is already deserialized by the TableClient)
        var queryResult = _table.Query<WishlistTableModel>();

        // transform list of WishlistTableModel objects to list of WishlistModel
        var resultList = queryResult.Select(row => WishlistModel.FromWishlistTableModel(row)).ToList();

        // return successfull response
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(resultList);
        return response;
    }


    // add game to wishlist
    [OpenApiOperation(operationId: "createGameinWishlist", tags: new[] {"games"}, Summary = "Add game to wishlist", Description = "Add a game to the wishlist.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(WishlistModel))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(WishlistModel))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorModel))]
    [Function("HTTPAddGameToWishlist")]
    public async Task<HttpResponseData> AddGameToWiishlist([HttpTrigger(AuthorizationLevel.Function, "post", Route = "game")] HttpRequestData request)
    {
        _logger.LogInformation($"[{request.FunctionContext.InvocationId}] Processing request for adding game endpoint.");

        // deserialize request body into WishlistModel object
        var addGameReq = await request.ReadFromJsonAsync<WishlistModel>();

        // if request body cannot be deserialized or is null, return an HTTP 400
        if (addGameReq == null)
            return request.CreateResponse(HttpStatusCode.BadRequest);

        // if id from game add request already exists -> return an HTTP 400
        if (_table.GetEntityIfExists<WishlistTableModel>(partitionKey: string.Empty, rowKey: addGameReq.Id).HasValue)
            return request.CreateResponse(HttpStatusCode.BadRequest);

        // transform WishlistModel into WishlistTableModel and write row to table; partition + row key need to be unique!
        var createTableRow = await _table.AddEntityAsync<WishlistTableModel>(new()
        {
            RowKey = addGameReq.Id,
            Name = addGameReq.Name,
            Url = addGameReq.Url,
            ReleaseDate = addGameReq.ReleaseDate

        });

        // return error if transaction in table storage unsuccessfull
        if (createTableRow.IsError)
        {
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync<ErrorModel>(new
            (
                Error: "TableTransactionError",
                ErrorMessage: "There was a problem executing the table transaction."
            ));
            return errorResponse;
        }

        // serialize requested WishlistModel to json and return to client, when request successfull
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(addGameReq);
        return response;
    }

    // delete specific game by isbn
    [OpenApiOperation(operationId: "delete game", tags: new[] {"games"}, Summary = "Delete game from wishlist", Description = "Delete a game by id from Wishlist.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Summary = "Id of the to be deleted game")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Summary = "Empty response if sucessfull.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorModel))]
    [Function("HTTPDeleteGame")]
    public async Task<HttpResponseData> DeleteGame([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "game/{id}")] HttpRequestData request, string id)
    {
        _logger.LogInformation($"[{request.FunctionContext.InvocationId}] Processing request to delete specific game by its id with the id {id}.");

        // try to delete game with given isbn
        var deleteResult = _table.DeleteEntity(partitionKey: string.Empty, rowKey: id);

        // return HTTP 500 if deletion unsucessfull
        if (deleteResult.IsError)
        {
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync<ErrorModel>(new
            (
                ErrorMessage: $"There was an error deleting the game with id {id}: {deleteResult.ReasonPhrase}.",
                Error: "GameDeletionError"
            ));
            return errorResponse;
        }

        // return sucessfull response as HTTP 204
        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
