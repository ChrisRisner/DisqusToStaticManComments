using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text;
using System.Security.Cryptography;

namespace commentImporter
{
    class Program
    {
        class ThreadObj {
            public string ThreadId { get; set; }
            public string ThreadName { get; set; }
            public List<Post> Posts { get; set; }
        }

        class Post {
            public string ParentPostId { get; set; }
            public string ThreadTitle { get; set; }
            public string ThreadId { get; set; }
            public ThreadObj ParentThread { get; set; }
            public string PostId { get; set; }
            public string Parent { get; set; }
            public List<Post> ChildrenPosts { get; set; }
            public Post ParentPost { get; set; }
            public string Message { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Url { get; set; }
            public string ReplyingTo { get; set; }
            public string Hidden { get; set; }
            public string Date { get; set; }
            public string Tags { get; set; }
            public string dateInMillis { get; set; }
            public bool Processed { get; set; }
            public string PostOrderDebug { get; set; }
            
            public string GetFileText() {
                StringBuilder sb = new StringBuilder();
                sb.Append("_id: ");
                sb.AppendLine(Guid.NewGuid().ToString());
                sb.Append("_parent: /");
                sb.AppendLine(this.ParentThread.ThreadName);
                sb.Append("message: \"");
                //Fix any characters that need escaping
                sb.Append(this.Message.Replace("\\", "\\\\").Replace("\"", "\\\""));
                if (this.Message.Contains("\"")) {
                    sb.Append("");
                }
                sb.AppendLine("\"");
                sb.Append("name: ");
                sb.AppendLine(this.Name.TrimStart('@'));
                sb.Append("email: ");
                sb.AppendLine(CalculateMD5Hash(this.Email));
                sb.Append("url: '");
                sb.Append(this.Url);
                sb.AppendLine("'");
                sb.Append("replying_to: '");
                sb.Append(this.ReplyingTo);
                sb.AppendLine("'");
                sb.AppendLine("hidden: ''");
                sb.Append("date: '");
                sb.Append(this.Date);
                sb.Append("'");
                return sb.ToString();                
            }
        }

        /**
        Used to calcualte MD5 hashes for hashing email addresses
         */
        public static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /***
        Update any post children to be part of the same thread (only 1 deep replies)
         */
         static void processPostsChildren(Post post, int replyingTo) {
            if (post.ChildrenPosts.Count > 0) {
                foreach (var childPost in post.ChildrenPosts) {
                    childPost.ReplyingTo = replyingTo.ToString();
                    childPost.Processed = true;
                    processPostsChildren(childPost, replyingTo);
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Comment Processing");
            //Create CommentFiles directory            
            if (!Directory.Exists("CommentFiles")) {
                Directory.CreateDirectory("CommentFiles");
                Console.WriteLine("CommentFiles directory created");
            } else {
                Console.WriteLine("CommentFiles directory already created");
            }
            //Read in comment XML from comments-source.xml file
            XElement root = XElement.Load("comments-source.xml");
            if (root != null) {
                Console.WriteLine("Xml has been read");
            } else {
                Console.WriteLine("Xml was empty or could not be read");
            }

            XNamespace dsq = XNamespace.Get("http://disqus.com/disqus-internals");
            Dictionary<string, string> threadNameAndIds = new Dictionary<string, string>();
            List<Post> listPosts = new List<Post>();
            List<ThreadObj>  listThreads = new List<ThreadObj>();
            foreach (var element in root.Elements()) {
                if (element.Name.LocalName == "thread") {
                    //Process Thread elements
                    var threadId = element.Attribute(dsq + "id").Value;
                    Console.WriteLine("Processing thread: " + threadId);
                    var idElement = element.Elements().Where(e => e.Name.LocalName == "id").First();
                    var idElementValue = idElement.Value;
                    threadNameAndIds.Add(threadId, idElementValue);
                    listThreads.Add(new ThreadObj() { ThreadId = threadId, ThreadName = idElementValue, Posts = new List<Post>() });
                    //Create directories for threads
                    if (!Directory.Exists("CommentFiles/" + idElementValue)) {
                        Console.WriteLine("Creating directory: " + idElementValue);
                        Directory.CreateDirectory("CommentFiles/" + idElementValue);
                    }
                } else if (element.Name.LocalName == "post") {
                    //Process Post elements
                    var post = new Post();
                    post.ChildrenPosts = new List<Post>();
                    post.Processed = false;                    
                    var postId = element.Attribute(dsq + "id").Value;
                    post.PostId = postId;
                    var postThreadElement = element.Elements().Where(e => e.Name.LocalName == "thread").First();
                    var postThreadId = postThreadElement.Attribute(dsq + "id").Value;
                    post.ThreadId = postThreadId;
                    Console.WriteLine("Processing Post: " + postId + "  for thread: " + postThreadId + "  with title: " + threadNameAndIds[postThreadId]);                     
                    var parentPostElement = element.Elements().Where(e => e.Name.LocalName == "parent").FirstOrDefault();
                    if (parentPostElement != null) {
                        var postParentPostId = parentPostElement.Attribute(dsq + "id").Value;
                        Console.WriteLine("With parent post: " + postParentPostId);
                        post.ParentPostId = postParentPostId;
                        post.ParentPost = listPosts.Where(p => p.PostId == postParentPostId).FirstOrDefault();
                        post.ParentPost.ChildrenPosts.Add(post);
                    }
                    var createdAtElement = element.Elements().Where(e => e.Name.LocalName == "createdAt").First();
                    post.Date = createdAtElement.Value;
                    var dt = DateTime.Parse(post.Date);
                    post.dateInMillis = new DateTimeOffset(dt).ToUnixTimeMilliseconds().ToString();                    
                    var authorElement = element.Elements().Where(e => e.Name.LocalName == "author").First();
                    var authorEmailElement = authorElement.Elements().Where(e => e.Name.LocalName == "email").First();
                    var authorNameElement = authorElement.Elements().Where(e => e.Name.LocalName == "name").First();
                    post.Email = authorEmailElement.Value;
                    post.Name = authorNameElement.Value;
                    var messageElement = element.Elements().Where(e => e.Name.LocalName == "message").First();
                    post.Message = messageElement.Value;
                    post.ParentThread = listThreads.Where(t => t.ThreadId == postThreadId).FirstOrDefault();
                    post.ParentThread.Posts.Add(post);
                    if (post.ParentThread.ThreadName == "Custom-Authentication-with-Azure-Mobile-Services-and-LensRocket") {
                        Console.WriteLine("DT: " + post.Date);
                        Console.WriteLine("MS: " + post.dateInMillis);
                    }
                    listPosts.Add(post);
                }
            }
            //Update replyingTo values for all threaded Posts
            foreach (var thread in listThreads) {
                int replyingToCount = 1;
                foreach (var post in thread.Posts.Where (p => p.ParentPost == null)) {                    
                    post.PostOrderDebug = replyingToCount.ToString();
                    if (post.ChildrenPosts.Count > 0) {
                        processPostsChildren(post, replyingToCount);
                    }
                    post.Processed = true;
                    replyingToCount++;
                }
            }
            //Check to see if any posts were not processed
            foreach (var post in listPosts) {
                if (!post.Processed) {
                    Console.WriteLine("Post not processed");
                }
            }
            //Create Post files
            int threadCount = 0;
            int postCount = 0;
            foreach (var thread in listThreads) {
                threadCount++;
                foreach (var post in thread.Posts) {
                    postCount++;                   
                    if (!string.IsNullOrEmpty(thread.ThreadName)) { 
                        File.AppendAllText("CommentFiles/" + thread.ThreadName + "/comment-" + post.dateInMillis + ".yml", post.GetFileText());
                    }
                    else {
                        Console.WriteLine("Empty ID: " + thread.ThreadId);
                    }
                }
            }
            Console.WriteLine("Total threads: " + threadCount);
            Console.WriteLine("Total posts: " + postCount);
            var commentDirs = Directory.EnumerateDirectories("CommentFiles/");
            //Clean up only empty directories
            foreach (var commentDir in commentDirs) {
                if (Directory.GetFiles(commentDir).Length == 0) {
                    Console.WriteLine("Removing directory: " + commentDir);                    
                    Directory.Delete(commentDir, true);
                    
                }
            }
        }
    }
}
