# Ralph Loop Workflow

The Ralph Loop workflow is an automated iterative development process that helps manage complex tasks through structured learning cycles.

## Overview

When you create an issue with the `ralph-task` label, the workflow automatically:

1. **Generates a Product Requirements Document (PRD)** from the issue content
2. **Maintains a learnings document** that tracks progress across iterations
3. **Executes iterative work** until completion requirements are met
4. **Cleans up** by removing the learnings document when the task is complete

## How to Use

### 1. Create a Ralph Task Issue

Use the "Ralph Task" issue template when creating a new issue. Fill in:

- **Task Description**: What you want to accomplish
- **Requirements**: Specific requirements for the task
- **Completion Requirement**: Criteria that define when the task is complete (optional but recommended)
- **Maximum Iterations**: How many iterations to run (defaults to 10)
- **Additional Context**: Any extra information

### 2. The Workflow Initializes

Once the issue is created with the `ralph-task` label:

- A PRD is generated at `docs/ralph-tasks/issue-{number}-prd.md`
- A learnings document is created at `docs/ralph-tasks/issue-{number}-learnings.md`
- The workflow posts a comment confirming initialization

### 3. Iterative Process

The workflow will:

- Execute iterations of work
- Update the learnings document with progress
- Check against completion requirements
- Continue until:
  - Completion requirements are met, OR
  - Maximum iterations are reached

### 4. Completion

When you determine the task is complete:

1. Reply to the completion check comment with: `@github-actions mark-complete`
2. The workflow will:
   - Remove the learnings document (temporary working doc)
   - Keep the PRD for historical reference
   - Add a `ralph-complete` label to the issue

## Documents

### Product Requirements Document (PRD)

- **Location**: `docs/ralph-tasks/issue-{number}-prd.md`
- **Purpose**: Permanent record of the task requirements and context
- **Lifecycle**: Created at start, kept permanently

### Learnings Document

- **Location**: `docs/ralph-tasks/issue-{number}-learnings.md`
- **Purpose**: Track progress, learnings, and decisions across iterations
- **Lifecycle**: Created at start, removed when task is complete

## Missing Completion Requirement

If you don't specify a completion requirement in the issue:

1. The workflow will post a comment asking for clarification
2. Reply to that comment with your completion criteria
3. The workflow will then begin iterations

## Configuration

### Default Settings

- **Maximum Iterations**: 10 (customizable per issue)
- **Completion Requirement**: Must be specified or will prompt for it

### Customization

You can customize the maximum iterations for each task in the issue template. The workflow will respect this limit.

## Examples

### Example Completion Requirements

Good completion requirements are:

- ✅ "All unit tests pass with >80% code coverage"
- ✅ "Feature X is implemented and documented with examples"
- ✅ "Build passes on all platforms (Linux, macOS, Windows)"
- ✅ "Performance benchmark shows <100ms response time"

Poor completion requirements:

- ❌ "When it's done" (too vague)
- ❌ "Make it better" (not measurable)
- ❌ "Fix the bug" (no specific criteria)

### Example Task Flow

1. Create issue with ralph-task label
2. Workflow creates PRD and learnings docs
3. Iteration 1: Initial implementation
4. Iteration 2: Add tests
5. Iteration 3: Fix failing tests
6. Iteration 4: Add documentation
7. Completion check: All requirements met
8. Mark complete: Learnings removed, PRD preserved

## Workflow Triggers

The workflow can be triggered by:

- Creating an issue with the `ralph-task` label
- Adding the `ralph-task` label to an existing issue
- Commenting `@github-actions mark-complete` on a ralph-task issue
- Manual workflow dispatch (for re-running)

## Benefits

1. **Structured Learning**: Captures knowledge across iterations
2. **Clear Requirements**: Forces upfront definition of success criteria
3. **Automatic Documentation**: PRD is generated from issue content
4. **Progress Tracking**: Learnings document shows the journey
5. **Clean Completion**: Removes temporary docs when done

## Troubleshooting

### Workflow doesn't start

- Ensure the issue has the `ralph-task` label
- Check workflow permissions in repository settings

### Missing completion requirement prompt

- Reply to the workflow comment with your completion criteria
- The workflow will continue once criteria are provided

### Maximum iterations reached

- Review the learnings document
- Decide if task is complete or needs more iterations
- Can manually increase iterations and re-run

## Advanced Usage

### Manual Trigger

You can manually trigger the workflow:

1. Go to Actions → Ralph Loop
2. Click "Run workflow"
3. Enter the issue number
4. The workflow will process that issue

### Multiple Parallel Tasks

You can have multiple Ralph tasks running simultaneously. Each gets its own PRD and learnings document identified by issue number.

## Best Practices

1. **Be Specific**: Clear completion requirements lead to better results
2. **Reasonable Iterations**: Start with 10, adjust based on task complexity
3. **Review Learnings**: Check the learnings document regularly to track progress
4. **Update Requirements**: If requirements change, update the issue and PRD
5. **Clean Completion**: Always mark tasks complete to clean up learnings docs
