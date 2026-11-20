using Scattered.Services.Products.BusinessLogic.UseCases;
using Scattered.Services.Products.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Scattered.Services.Products.Api.Write.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductController
{
    private readonly IUpdateProductDetails _updateProductDetails;
    private readonly ICreateProductDetails _createProductDetails;

    public ProductController(IUpdateProductDetails updateProductDetails, ICreateProductDetails createProductDetails)
    {
        _updateProductDetails = updateProductDetails;
        _createProductDetails = createProductDetails;
    }

    [HttpPost(Name = "addproduct")]
    public async Task<IActionResult> AddProduct(CreateProductRequest createProductRequest)
    {
        CreateProductResponse result = await _createProductDetails.Execute(createProductRequest);

        return new CreatedResult(new Uri(result.Url), null);
    }


    [HttpPut("updateproductdetails/{id}")]
    public async Task<IActionResult> UpdateProductDetails(int id, ProductDetails productDetails)
    {
        bool result = await _updateProductDetails.Execute(id, productDetails);

        return new OkResult();
    }
}