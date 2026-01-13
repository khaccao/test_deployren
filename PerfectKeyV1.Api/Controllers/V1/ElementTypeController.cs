using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PerfectKeyV1.Application.DTOs.Layout;
using PerfectKeyV1.Application.Interfaces;

namespace PerfectKeyV1.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/layout/element-types")]
    [Authorize]
    public class ElementTypeController : ControllerBase
    {
        private readonly IElementTypeService _elementTypeService;

        public ElementTypeController(IElementTypeService elementTypeService)
        {
            _elementTypeService = elementTypeService;
        }

        /// <summary>
        /// Get all element types
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ElementTypeDto>), 200)]
        public async Task<IActionResult> GetAllElementTypes()
        {
            var elementTypes = await _elementTypeService.GetAllElementTypesAsync();
            return Ok(elementTypes);
        }

        /// <summary>
        /// Get element type by GUID
        /// </summary>
        [HttpGet("{guid}")]
        [ProducesResponseType(typeof(ElementTypeDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetElementType(Guid guid)
        {
            var elementType = await _elementTypeService.GetElementTypeByGuidAsync(guid);
            if (elementType == null)
                return NotFound("Element type not found");

            return Ok(elementType);
        }

        /// <summary>
        /// Create new element type
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(ElementTypeDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateElementType([FromBody] CreateElementTypeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var elementType = await _elementTypeService.CreateElementTypeAsync(request);
                return CreatedAtAction(nameof(GetElementType), new { guid = elementType.Guid }, elementType);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Update element type
        /// </summary>
        [HttpPut("{guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(ElementTypeDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateElementType(Guid guid, [FromBody] UpdateElementTypeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var elementType = await _elementTypeService.UpdateElementTypeAsync(guid, request);
                if (elementType == null)
                    return NotFound("Element type not found");

                return Ok(elementType);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Delete element type (soft delete)
        /// </summary>
        [HttpDelete("{guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteElementType(Guid guid)
        {
            var result = await _elementTypeService.DeleteElementTypeAsync(guid);
            if (!result)
                return NotFound("Element type not found");

            return Ok("Element type deleted successfully");
        }
    }
}