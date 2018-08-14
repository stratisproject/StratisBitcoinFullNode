Contributing to Stratis FullNode
================================

The Stratis team maintains a few guidelines, which are provided below. Many of these are straightforward.  
For any questions a Stratis team member will be happy to explain more.

We try to follow the [.Net core](https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/contributing.md) guidlines.

Contribution Guidelines
=======================

- [Copyright](#copyright) describes the licensing practices for the project.
- [General Contribution Guidance](#general-contribution-guidance) describes general contribution guidance, including more subjective stylistic guidelines.

General Contribution Guidance
=============================

There are several issues to keep in mind when making a change.

Adding Features
---------------
Before Adding new features please discuss that on public channels first (issue/slack).  
Don't start coding before we have a clear design and direction.
It's preferred to make small incremental commits (unless its impossible) and not a massive all in commit. 
Unit tests are a MUST! If the method you modify does not have any tests yet try to add them! We haven't written tests for all code due to delivery pressure but the goal is to have everything covered by tests.

Adding Unit Tests
-----------------
Test contributions are more than welcome!
Pick a class that does not have any tests and start testing it.
Have a look at our [testing guidelines](https://github.com/stratisproject/StratisBitcoinFullNode/blob/master/Documentation/testing-guidelines.md)
Use other tests for reference.

Typos and small changes
-----------------------
Typos are embarrassing! We will accept most PRs that fix typos. In order to make it easier to review your PR, please focus on a given component with your fixes or on one type of typo across the entire repository. If it's going to take >30 mins to review your PR, then we will probably ask you to chunk it up.

Commit Messages
---------------

Please format commit messages as follows (based on this [excellent post](http://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html)):

```
Summarize change in 50 characters or less

Provide more detail after the first line. Leave one blank line below the
summary and wrap all lines at 72 characters or less.

If the change fixes an issue, leave another blank line after the final
paragraph and indicate which issue is fixed in the specific format
below.

Fix #42
```

Also do your best to factor commits appropriately, i.e not too large with unrelated
things in the same commit, and not too small with the same small change applied N
times in N different commits. If there was some accidental reformatting or whitespace
changes during the course of your commits, please rebase them away before submitting
the PR.

DOs and DON'Ts
--------------

* **DO** follow our [coding style](./coding-style.md)
* **DO** follow our [code commenting policy and guidelines](./commenting-policy.md)
* **DO** give priority to the current style of the project or file you're changing even if it diverges from the general guidelines.
* **DO** include tests when adding new features. When fixing bugs, start with
  adding a test that highlights how the current behavior is broken.
* **DO** keep the discussions focused. When a new or related topic comes up
  it's often better to create new issue than to side track the discussion.
* **DO** blog and tweet (or whatever) about your contributions, frequently!
* **DO** favour fixing merge conflicts using `git rebase` over `git merge`.

* **DO NOT** send PRs for style changes. 
* **DON'T** surprise us with big pull requests. Instead, file an issue and start
  a discussion so we can agree on a direction before you invest a large amount
  of time.
* **DON'T** commit code that you didn't write. If you find code that you think is a good fit to add to .NET Core, file an issue and start a discussion before proceeding.
* **DON'T** submit PRs that alter licensing related files or headers. If you believe there's a problem with them, file an issue and we'll be happy to discuss it.

Copyright
=========

Source License
--------------

The Stratis licenses can be found under the [HERE](https://github.com/stratisproject/StratisBitcoinFullNode/blob/master/LICENSE) project.

Copying Files from Other Projects
---------------------------------

The following rules must be followed for PRs that include files from another project:

- The license of the file is [permissive](https://en.wikipedia.org/wiki/Permissive_free_software_licence).
- The license of the file is left in-tact.
- The contribution is correctly attributed in the [3rd party notices](../../THIRD-PARTY-NOTICES) file in the repository, as needed.

Porting Files from Other Projects
---------------------------------

There are many good algorithms implemented in other languages that would benefit the Stratis project. The rules for porting a Java file to C# , for example, are the same as would be used for copying the same file, as described above.

Contributing
------------

By contributing to this repository, you agree to license your work under the 
[Stratis](https://github.com/stratisproject/StratisBitcoinFullNode/blob/master/LICENSE) license unless specified otherwise at 
the top of the file itself. Any work contributed where you are not the original 
author must contain its license header with the original author(s) and source.
