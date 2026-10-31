using KamCo.Api.DTOs.Products;
using KamCo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KamCo.Api.Controllers;

/// <summary>
/// Product catalog. Reads are public (with search / filter / sort / paging);
/// writes require the Admin role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController(IProductService productService) : ControllerBase
{
    /// <summary>List products with optional search, category filter, sorting and paging.</summary>
    /// <param name="query">
    /// Examples:
    /// `/api/products?search=keyboard`,
    /// `/api/products?categoryId=1&amp;sortBy=price_asc`,
    /// `/api/products?page=2&amp;pageSize=5`
    /// </param>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get([FromQuery] ProductQueryParameters query)
        => Ok(await productService.GetAsync(query));

    /// <summary>Get one product by id.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id)
        => Ok(await productService.GetByIdAsync(id));

    /// <summary>Create a product. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto)
    {
        var created = await productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update a product. Admin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> Update(int id, UpdateProductDto dto)
        => Ok(await productService.UpdateAsync(id, dto));

    /// <summary>
    /// Delete a product. Admin only.
    /// If the product appears in past orders it is deactivated instead of deleted,
    /// so order history stays intact.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        await productService.DeleteAsync(id);
        return NoContent();
    }
}
