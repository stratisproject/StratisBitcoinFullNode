# Code Commenting Policy and Guidelines

**TLDR**: Go to [Rules](#rules) section and follow hard rules, try to follow soft rules where possible.

This document describes the policy on comments within source code files. The policy is enforced during code reviews.
No new code should be accepted to the code base if it does not comply this policy.

We are aware of the fact that a lot of the existing code does not comply with this policy. You can help us to improve 
the project by changing existing code to comply with this policy.


## Motivation

The main motivation is to spend time designing and writing the code and comments in order to save time of those 
who will later read the code, try to fix bugs in it, or otherwise use it.

Many people *know* that commenting the code is the right thing to do, yet they still create a lot of uncommented code.
The list of reasons why one did not comment the code is endless, but most common reasons are:

 * coding without comments is faster and we have deadlines,
 * well written code does not need a comment (code is far better describing what code does than English),
 * we can add comments later.

All these reasons and other excuses why the code is not commented make it harder for the future reader of the code 
to understand it. Yes, it is true that you should always try to write the code as if you could not write any comment to it. 
Having a well defined [code style](./coding-style.md) helps with that. But regardless how good your code is, you still need 
to add comments. Writing comments captures intent and this is something that raw code can not. If the code is buggy 
and there is no comment anywhere, how could you tell the code is buggy? If you are not the one who wrote it and you have 
no comments, you can only guess what was the intention of the author.

The design of the code and the itentions of its author are important knowledge and the knowledge has to be shared. 
Otherwise, it is hard for everyone to work with the code if the only person with that knowledge is not available.
Spend the time now to add comments to save the time to your collegues, or your future self, later. If you do that, you will 
find out that in order to produce the comment, you have to understand what you are doing. By trying to explain your intention, 
you will often find a problem in your design and you will actually be able to complete your task faster.

If you are interested to learn more about why to comment your code, there are almost unlimited number of resources on this 
topic on the Internet. You will find many blogs and even books talking about the necessity of comments.


## Rules

We have two types of rules. Hard rules are those that are required and your code will not be accepted if it violates 
them. Soft rules are more like guidelines - try to use them where possible. The reviewer can ask you to update your code 
if it violates a soft rule and it can be fixed easily.

 * **XMLDOC** - Use triple slash (XML documentation) comments for classes, interfaces, methods, parameters, fields, properties, structures, enums, etc. These comments are required - even for trivial code. *(HARD)*
   * **SUMMARY-1** - First paragraph in `<summary>` must be short, no more than 3 lines. If you find you need more than 2 lines, maybe you are not writing the summary anymore and you are already explaining details. *(HARD)*
   * **SUMMARY-2** - `<summary>` should contain information useful for someone who is trying to understand the code. Implementation details belong to `<remarks>`. *(SOFT)*
   * **METHODS** - Method comments must contain `<summary>`. If the method has input and/or output parameters `<param>` must be used for each of them. If the method returns anything else than `void` or `Task` (but not `Task<T>`),
it must contain `<returns>`. The order of the tags is `<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`, `<example>`, `<seealso>`. *(HARD)*
   * **FORMAT** - Use `<para>`, `<see>`, `<seealso>`, `<c>`, `<code>`, `<list>` to make the comment structure better. *(SOFT)*
   * **INHERITDOC** - Use `<inheritdoc />` if the comment already exists on lower level (base class, interface, virtual/abstract method). But feel free add additional comments if the base comment is not sufficient. *(HARD)*
 * **INLINE-1** - Use double slash (inline) comments for function body, variables, and inline comments. *(HARD)*
 * **INLINE-2** - Double slash comments are optional and should be used to explain complicated segments of the code. *(SOFT)*
 * **INLINE-3** - Do not use double slash comments for trivial constructs. *(HARD)*
 * **INTENT** - Try to describe the intent or the big picture rather than what the code does. *(SOFT)*
 * **INTERACTIONS** - Explain the interactions of the code with other parts of the system. This includes contracts and limitations that has to be respected - e.g. code protected by locks, special values of parameters, lower and upper bounds of parameters, etc. *(SOFT)*
 * **UPDATES** - Keep comments up to date when editing commented code. If editing uncommented code, add comments with the change. *(HARD)*
 * **SENTENCES** - Try to write comments as sentences - with capital letter at the beginning and dot (question mark, exclamation mark) at the end. Omitting verbs is OK. *(SOFT)*
 * **ADD-VALUE** - Every comment should add value, even if the comment is trivial on trivial code (XML documentation comments). *(SOFT)* 


## Examples

### Fibonacci 

The following code violates **XMLDOC.SUMMARY-2**, **INLINE-3** and **INTENT** rules:

```
/// <summary>
/// Calculates the number which is at N-th position in the sequence that starts 
/// with 1, 1 and the next element is always the sum of two previous elements.
/// </summary>
/// <param name="n">N.</param>
/// <returns>N-th element the Fibonacci sequence.</returns>
public static int Fib(int n)
{
    int previous = -1;
    int current = 1;
    int index = 1;
    int element = 0;

    while (index++ <= n)
    {
        // Calculates previous element + current element
        element = previous + current;
        previous = current;
        current = element;
    }
    return element;
}
```

The XML documentation should rather look like this:

```
/// <summary>
/// Calculates N-th element of the Fibonacci sequence using iteration method.
/// </summary>
```

This summary is much better because the Fibonacci sequence is well known and one can search its details.
If a less known, or custom, algorithm was used, remarks (not summary) section could be used to explain how it works.


```
/// <param name="n">Index of the element to return. The index of the first element in the sequence is 1. The highest supported index is 47.</param>
```

This parameter description is valuable mostly because of the documented limits of the implementation. 

The inline comment `// Calculates previous element + current element` is completely unnecessary as it adds nothing to the reader.


### Name

Consider the following variants of the code inside `Activity` class.

```
/// <summary>Name.</summary>
private string Name;
```

```
/// <summary>Name of the activity.</summary>
private string Name;
```

```
/// <summary>Name of the activity. This is used for logging purposes only.</summary>
private string Name;
```

```
/// <summary>Name of the activity as it is shown to the user in XyzDialog.</summary>
private string Name;
```

The first one is too minimalistic and does not help the developer very much. The second one is much better because IntelliSense 
will remind the coder working with an instance of the object that he is working with the activity and not something else.
Variants 3 and 4 are the best as they add additional context that might be important. 
Variants 2, 3, and 4 should be accepted, but variant 1 should not be accepted for violating **ADD-VALUE** rule.


### Locks

When you work with shared resources in multithreaded environment, you want to document all contracts so that the reviewer can check 
that the code is correct.

```
/// <summary>
/// Assigns a download task to a specific peer.
/// </summary>
/// <param name="peer">Peer to be assigned the new task.</param>
/// <param name="blockHash">Hash of the block to download from <paramref name="peer"/>.</param>
/// <returns><c>true</c> if the block was assigned to the peer, <c>false</c> in case the block has already been assigned to someone.</returns>
/// <remarks>The caller of this method is responsible for holding <see cref="lockObject"/>.</remarks>
private bool AssignDownloadTaskToPeerLocked(BlockPullerBehavior peer, uint256 blockHash)
...
```

We also use "Locked" suffix to the method name, but that is a matter of the code style. From comments point of view, the remarks section 
is very important for everyone who wants to call the method. However, for someone who only wants to understand the code flow, 
it is not as important and thus the contract description is not a part of the summary.

