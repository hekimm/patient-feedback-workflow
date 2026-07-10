using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Models.ViewModels;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER", "UNIT_MANAGER")]
public class FeedbackController : BaseController
{
    private readonly FeedbackRepository _feedbackRepository;

    public FeedbackController(FeedbackRepository feedbackRepository)
    {
        _feedbackRepository = feedbackRepository;
    }

    public async Task<IActionResult> Index(FeedbackFilter filter)
    {
        if (!filter.StartDate.HasValue)
            filter.StartDate = DateTime.Now.AddDays(-30);
        
        if (!filter.EndDate.HasValue)
            filter.EndDate = DateTime.Now;

        var feedbacks = await _feedbackRepository.GetFeedbackListAsync(
            filter, HttpContext.GetUserId(), HttpContext.GetRoleCode());

        var model = new FeedbackListViewModel
        {
            Feedbacks = feedbacks,
            Filter = filter
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var feedback = await _feedbackRepository.GetFeedbackDetailAsync(
            id, HttpContext.GetUserId(), HttpContext.GetRoleCode());
        
        if (feedback == null)
            return NotFound();

        feedback.Answers = await _feedbackRepository.GetAnswersAsync(id);
        feedback.RelatedCaseId = await _feedbackRepository.GetRelatedCaseIdAsync(id);

        return View(feedback);
    }
}
