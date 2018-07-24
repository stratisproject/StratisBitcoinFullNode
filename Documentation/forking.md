# Forking

To fork this repo, and continue to get patches and updates, you need to make sure you fetch updates correctly from the upstream repo.

```
# Fetch the changes from upstream repo
git fetch upstream

# View all branches
git branch -va
```

Depending of you are working with master as the primary branch or not, you can do the following to checkout your branch, and then
perform an merge with upstream branch.

```
git checkout master
git merge upstream/master
```

## Resources

[GitHub Standard Fork & Pull Request Workflow](https://gist.github.com/Chaser324/ce0505fbed06b947d962)

[Merging an upstream repository into your fork](https://help.github.com/articles/merging-an-upstream-repository-into-your-fork/)

[Long-Running Branches](https://github.com/dotnet/llilc/blob/master/Documentation/Long-Running-Branch-Workflow.md)
