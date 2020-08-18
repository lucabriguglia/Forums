﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Atlify.Data;
using Atlify.Models;
using Atlify.Models.Public;
using Atlify.Models.Public.Posts;
using Atlify.Models.Public.Topics;
using Atlify.Server.Services;
using Atlify.Domain;
using Atlify.Domain.PermissionSets;
using Atlify.Domain.Posts;
using Atlify.Domain.Posts.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlify.Server.Controllers.Public
{
    [Route("api/public/topics")]
    [ApiController]
    public class TopicsController : ControllerBase
    {
        private readonly IContextService _contextService;
        private readonly ITopicModelBuilder _topicModelBuilder;
        private readonly IPostModelBuilder _postModelBuilder;
        private readonly ITopicService _topicService;
        private readonly ISecurityService _securityService;
        private readonly AtlifyDbContext _dbContext;
        private readonly IPermissionModelBuilder _permissionModelBuilder;
        private readonly ILogger<TopicsController> _logger;

        public TopicsController(IContextService contextService,
            ITopicModelBuilder topicModelBuilder,
            IPostModelBuilder postModelBuilder,
            ITopicService topicService,
            ISecurityService securityService, 
            AtlifyDbContext dbContext, 
            IPermissionModelBuilder permissionModelBuilder, 
            ILogger<TopicsController> logger)
        {
            _contextService = contextService;
            _topicModelBuilder = topicModelBuilder;
            _postModelBuilder = postModelBuilder;
            _topicService = topicService;
            _securityService = securityService;
            _dbContext = dbContext;
            _permissionModelBuilder = permissionModelBuilder;
            _logger = logger;
        }

        [HttpGet("{forumSlug}/{topicSlug}")]
        public async Task<ActionResult<TopicPageModel>> Topic(string forumSlug, string topicSlug, [FromQuery] int? page = 1, [FromQuery] string search = null)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var model = await _topicModelBuilder.BuildTopicPageModelAsync(site.Id, forumSlug, topicSlug, new QueryOptions(search, page));

            if (model == null)
            {
                _logger.LogWarning("Topic not found.", new
                {
                    SiteId = site.Id,
                    ForumSlug = forumSlug,
                    TopicSlug = topicSlug,
                    User = User.Identity.Name
                });

                return NotFound();
            }

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, model.Forum.Id);

            var canRead = _securityService.HasPermission(PermissionType.Read, permissions);

            if (!canRead)
            {
                _logger.LogWarning("Unauthorized access to topic", new
                {
                    SiteId = site.Id,
                    ForumSlug = forumSlug,
                    TopicSlug = topicSlug,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            model.CanEdit = _securityService.HasPermission(PermissionType.Edit, permissions) && !member.IsSuspended;
            model.CanReply = _securityService.HasPermission(PermissionType.Reply, permissions) && !member.IsSuspended;
            model.CanDelete = _securityService.HasPermission(PermissionType.Delete, permissions) && !member.IsSuspended;
            model.CanModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;

            return model;
        }

        [HttpGet("{forumId}/{topicId}/replies")]
        public async Task<ActionResult<PaginatedData<TopicPageModel.ReplyModel>>> Replies(Guid forumId, Guid topicId, [FromQuery] int? page = 1, [FromQuery] string search = null)
        {
            var site = await _contextService.CurrentSiteAsync();

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, forumId);

            var canRead = _securityService.HasPermission(PermissionType.Read, permissions);

            if (!canRead)
            {
                _logger.LogWarning("Unauthorized access to topic replies", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            var result = await _topicModelBuilder.BuildTopicPageModelRepliesAsync(topicId, new QueryOptions(search, page));

            return result;
        }

        [Authorize]
        [HttpGet("{forumId}/new-topic")]
        public async Task<ActionResult<PostPageModel>> NewTopic(Guid forumId)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var model = await _postModelBuilder.BuildNewPostPageModelAsync(site.Id, forumId);

            if (model == null)
            {
                _logger.LogWarning("Forum for new topic not found.", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    User = User.Identity.Name
                });

                return NotFound();
            }

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, model.Forum.Id);
            var canPost = _securityService.HasPermission(PermissionType.Start, permissions) && !member.IsSuspended;

            if (!canPost)
            {
                _logger.LogWarning("Unauthorized access to new topic", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            return model;
        }

        [Authorize]
        [HttpGet("{forumId}/edit-topic/{topicId}")]
        public async Task<ActionResult<PostPageModel>> EditTopic(Guid forumId, Guid topicId)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var model = await _postModelBuilder.BuildEditPostPageModelAsync(site.Id, forumId, topicId);

            if (model == null)
            {
                _logger.LogWarning("Topic to edit not found.", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return NotFound();
            }

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, forumId);
            var canEdit = _securityService.HasPermission(PermissionType.Edit, permissions) && !member.IsSuspended;
            var canModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;
            var authorized = canEdit && model.Topic.MemberId == member.Id && !model.Topic.Locked || canModerate;

            if (!authorized)
            {
                _logger.LogWarning("Unauthorized access to edit topic", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            return model;
        }

        [Authorize]
        [HttpPost("create-topic")]
        public async Task<ActionResult> CreateTopic(PostPageModel model)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, model.Forum.Id);
            var canPost = _securityService.HasPermission(PermissionType.Start, permissions) && !member.IsSuspended;

            if (!canPost)
            {
                _logger.LogWarning("Unauthorized access to create topic", new
                {
                    SiteId = site.Id,
                    ForumId = model.Forum?.Id,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            var command = new CreateTopic
            {
                ForumId = model.Forum.Id,
                Title = model.Topic.Title,
                Content = model.Topic.Content,
                Status = StatusType.Published,
                SiteId = site.Id,
                MemberId = member.Id
            };

            var slug = await _topicService.CreateAsync(command);

            return Ok(slug);
        }

        [Authorize]
        [HttpPost("update-topic")]
        public async Task<ActionResult> UpdateTopic(PostPageModel model)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var command = new UpdateTopic
            {
                Id = model.Topic.Id,
                ForumId = model.Forum.Id,
                Title = model.Topic.Title,
                Content = model.Topic.Content,
                Status = StatusType.Published,
                SiteId = site.Id,
                MemberId = member.Id
            };

            var topicInfo = await _dbContext.Posts
                .Where(x =>
                    x.Id == command.Id &&
                    x.TopicId == null &&
                    x.ForumId == command.ForumId &&
                    x.Forum.Category.SiteId == command.SiteId &&
                    x.Status != StatusType.Deleted)
                .Select(x => new { x.MemberId, x.Locked})
                .FirstOrDefaultAsync();

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, model.Forum.Id);
            var canEdit = _securityService.HasPermission(PermissionType.Edit, permissions) && !member.IsSuspended;
            var canModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;
            var authorized = canEdit && topicInfo.MemberId == member.Id && !topicInfo.Locked || canModerate;

            if (!authorized)
            {
                _logger.LogWarning("Unauthorized access to update topic", new
                {
                    SiteId = site.Id,
                    ForumId = model.Forum?.Id,
                    TopicId = model.Topic?.Id,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            var slug = await _topicService.UpdateAsync(command);

            return Ok(slug);
        }

        [Authorize]
        [HttpPost("pin-topic/{forumId}/{topicId}")]
        public async Task<ActionResult> PinTopic(Guid forumId, Guid topicId, [FromBody] bool pinned)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var command = new PinTopic
            {
                Id = topicId,
                ForumId = forumId,
                Pinned = pinned,
                SiteId = site.Id,
                MemberId = member.Id
            };

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, forumId);
            var canModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;

            if (!canModerate)
            {
                _logger.LogWarning("Unauthorized access to pin topic", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            await _topicService.PinAsync(command);

            return Ok();
        }

        [Authorize]
        [HttpPost("lock-topic/{forumId}/{topicId}")]
        public async Task<ActionResult> LockTopic(Guid forumId, Guid topicId, [FromBody] bool locked)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var command = new LockTopic
            {
                Id = topicId,
                ForumId = forumId,
                Locked = locked,
                SiteId = site.Id,
                MemberId = member.Id
            };

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, forumId);
            var canModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;

            if (!canModerate)
            {
                _logger.LogWarning("Unauthorized access to lock topic", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            await _topicService.LockAsync(command);

            return Ok();
        }

        [Authorize]
        [HttpDelete("delete-topic/{forumId}/{topicId}")]
        public async Task<ActionResult> DeleteTopic(Guid forumId, Guid topicId)
        {
            var site = await _contextService.CurrentSiteAsync();
            var member = await _contextService.CurrentMemberAsync();

            var command = new DeleteTopic
            {
                Id = topicId,
                ForumId = forumId,
                SiteId = site.Id,
                MemberId = member.Id
            };

            var topicMemberId = await _dbContext.Posts
                .Where(x =>
                    x.Id == command.Id &&
                    x.TopicId == null &&
                    x.ForumId == command.ForumId &&
                    x.Forum.Category.SiteId == command.SiteId &&
                    x.Status != StatusType.Deleted)
                .Select(x => x.MemberId)
                .FirstOrDefaultAsync();

            var permissions = await _permissionModelBuilder.BuildPermissionModelsByForumId(site.Id, forumId);
            var canDelete = _securityService.HasPermission(PermissionType.Delete, permissions) && !member.IsSuspended;
            var canModerate = _securityService.HasPermission(PermissionType.Moderate, permissions) && !member.IsSuspended;
            var authorized = canDelete && topicMemberId == member.Id || canModerate;

            if (!authorized)
            {
                _logger.LogWarning("Unauthorized access to delete topic", new
                {
                    SiteId = site.Id,
                    ForumId = forumId,
                    TopicId = topicId,
                    User = User.Identity.Name
                });

                return Unauthorized();
            }

            await _topicService.DeleteAsync(command);

            return Ok();
        }
    }
}