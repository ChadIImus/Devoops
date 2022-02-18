﻿#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.EntityFrameworkCore;
using Minitwit.Models.Context;
using Minitwit.Models.DTO;
using Minitwit.Models.Entity;
using Minitwit.Services;

namespace Minitwit.Controllers
{
    public class UsersController : Controller
    {
        private readonly MinitwitContext _context;
        private readonly IUserService _userService;
        private const int PER_PAGE = 30;
        public UsersController(MinitwitContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        [HttpGet]
        [Route("[controller]/timeline")]
        public async Task<IActionResult> PrivateTimeline(int id)
        {
            //Todo get the id from authentication

            //Todo Redirect to public timeline
            if (!UserExists(id)) return RedirectToAction(nameof(PublicTimeline));

            var postsAndFollows = await _context.Users
                .Include(u => u.Messages)
                .Include(u => u.Follows)
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Messages,
                    u.Follows
                 })
                .FirstOrDefaultAsync();

            //Almost certain you need this subquery to find the posts of the people the user follows
            var followsPosts = await _context.Posts
                .Include(p => p.Author)
                .Where(p => !p.Flagged)
                .Where(p =>  postsAndFollows.Follows.Exists(u => u.Id  == p.Author.Id))
                .Concat(postsAndFollows.Messages)
                .OrderByDescending(p => p.PublishDate)
                .Take(PER_PAGE)
                .ToListAsync();

            return Ok(followsPosts);
        }

        [HttpGet]
        [Route("[controller]/timeline/public")]
        public async Task<IActionResult> PublicTimeline()
        {

            var posts = await _context.Posts
                .Where(p => !p.Flagged)
                .OrderByDescending(p => p.PublishDate)
                .Take(PER_PAGE)
                .ToListAsync();

            return Ok(posts);
        }

        [HttpGet]
        [Route("[controller]/timeline/{username}")]
        public async Task<IActionResult> UserTimeline(string username)
        {
            var user = await _context.Users
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();
            if (user == null) return NotFound($"Username with name {username} not found");

            var postsAndIsFollowing = await _context.Users
                .Include(u => u.Messages)
                .Include(u => u.Follows)
                .Where(u => u.Username == username)
                .Select(u => new{
                      messages = u.Messages
                          .OrderByDescending(p => p.PublishDate)
                          .Take(PER_PAGE),
                      follows = u.Follows.Exists(u => u.Id == user.Id)
                }
                )
                .FirstOrDefaultAsync();

            return Ok(new
            {
                user,
                postsAndIsFollowing.messages,
                postsAndIsFollowing.follows
            });
        }

        [HttpPost]
        [Route("[Controller]/{username}/Follow")]
        public async Task<IActionResult> Follow(string username, int id)
        {
            //Todo get the id from authentication

            User whom = _context.Users
                .Include(u => u.FollowedBy)
                .FirstOrDefault(u => u.Id == id);

            User who = _context.Users
                .Include(u => u.Follows)
                .FirstOrDefault(u => u.Username == username);

            if (who == null) return NotFound($"User with name {username} not found");

            whom.FollowedBy.Add(who);
            who.Follows.Add(whom); 
            await _context.SaveChangesAsync();

            //Todo Redirect to private timeline
            return RedirectToAction(nameof(PrivateTimeline));
        }


        //Todo If this is not too much trouble in the front end, lets simply make 1 endpoint and a boolean for follow/unfollow
        [HttpPost]
        [Route("[Controller]/{username}/unFollow")]
        public async Task<IActionResult> UnFollow(string username, int id)
        {
            //Todo get the id from authentication

            User whom = _context.Users
                .Include(u => u.FollowedBy)
                .FirstOrDefault(u => u.Id == id);

            User who = _context.Users
                .Include(u => u.Follows)
                .FirstOrDefault(u => u.Username == username);

            if (who == null) return NotFound($"User with name {username} not found");

            whom.FollowedBy.Remove(who);
            who.Follows.Remove(whom);
            await _context.SaveChangesAsync();

            //Todo Redirect to private timeline
            return RedirectToAction(nameof(PrivateTimeline));
        }

        [HttpPost]
        [Route("[controller]/post")]
        public async Task<IActionResult> post([FromBody] string text, int id)
        {
            //Todo get the id from authentication

            if (!ModelState.IsValid) return BadRequest("Text is required");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            Message newMessage = new Message()
            {
                Author = user,
                Text = text,
                PublishDate = DateTime.UtcNow
            };

            _context.Posts.Add(newMessage);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PublicTimeline));
        }

        //Todo register
        
        //Todo login

        //Todo logoff

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody]UserCreationDTO userDTO)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState.Values);

            return await _userService.CreateUser(userDTO) switch
            {
                Result.Conflict => StatusCode(413),
                Result.Created => StatusCode(201),
                //should never happen
                _ => throw new Exception()
            };
        }
        
        public async Task<IActionResult> MigrationCreate(UserDTO user)
        {
            User newUser = new User
            {
                Username = user.Username,
                PasswordHash = user.PasswordHash,
                Salt = user.Salt,
                Email = user.Email
            };
            _context.Add(newUser);
            await _context.SaveChangesAsync();
            return Ok();
        }

        public async Task<IActionResult> MigrationFollow(FollowDTO followDto)
        {
            User whom = _context.Users
                .Include(u => u.FollowedBy)
                .FirstOrDefault(u => u.Username  == followDto.Whom);

            User who = _context.Users
                .Include(u => u.Follows)
                .FirstOrDefault(u => u.Username == followDto.Who);

            if (whom != null && who != null)
            {
                whom.FollowedBy.Add(who);
                who.Follows.Add(whom);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }
        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Username,PasswordHash,Email")] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
