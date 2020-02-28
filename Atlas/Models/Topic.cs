﻿using System;
using System.Collections.Generic;

namespace Atlas.Models
{
    public class Topic
    {
        public Guid Id { get; set; }
        public Guid ForumId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public Guid MemberId { get; set; }
        public int RepliesCount { get; set; }

        public virtual Forum Forum { get; set; }
        public virtual Member Member { get; set; }

        public virtual ICollection<Reply> Replies { get; set; }

        public Topic()
        {
            
        }
    }
}
