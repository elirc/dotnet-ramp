# 11. Form state, request validation, and date seams

Read [ExpenseFormPage](../../frontend/src/pages/ExpenseFormPage.jsx), [request DTOs](../../backend/Dtos/Dtos.cs), and [ExpenseService](../../backend/Services/ExpenseService.cs). This chapter follows a value from browser input through JSON binding into a mutable expense. It separates convenience validation in the UI, request-shape validation, domain policy, and lifecycle permission.

## A form is a state machine before it is a request

The form stores merchant, description, amount, currency, category, and expense date in one object. In edit mode it fetches the expense and replaces those fields from the response. On submit it prevents the default event, clears prior errors, sets busy, creates a payload with parseFloat for amount, and chooses POST or PUT based on whether an id exists.

The submit button is disabled while busy, which reduces ordinary repeated clicks through that control. It does not create server-side idempotency or prevent a second client from sending another request. Treat busy as a user-interface state, not a durable command identity.

After a successful save response, the component navigates using saved.id. That assumes the response is a nonnull object with a usable identifier. The fetch-wrapper chapter showed that some successful HTTP responses could still produce null or parsing errors. A form contract test should therefore check both request construction and response interpretation.

## Input values and domain values have different types

Browser input state often contains strings even when the eventual domain value is decimal. The amount input updates form.amount from event.target.value, and submit converts it with parseFloat. A loaded expense may instead place a numeric amount into the same form field. The component handles both through its selected rendering and conversion path.

A useful worksheet records raw input text, converted JavaScript number, serialized JSON value, bound decimal, and stored amount. These are five distinct representations. For ordinary values such as twelve dollars and fifty cents they appear straightforward, but malformed strings, empty values, and precision boundaries can expose differences.

The browser number input and required attributes constrain ordinary interactive submission, but a direct API caller bypasses those controls. Likewise, calling the service directly bypasses MVC model validation unless the test invokes it explicitly. The test boundary determines which guards are active.

Do not claim that the decimal domain type prevents every earlier representation issue. The conversion and serialization already occurred before binding. A robust contract can reject unsupported precision, define rounding, or use an exact textual amount representation, but those are design decisions that need compatible client and server changes.

## Compare the visible constraints

The form limits merchant to two hundred characters and description to one thousand, matching the selected DTO length limits. It uses a positive minimum and cent step for amount, while the DTO declares a positive lower bound and a one-million upper bound. The form does not show that same upper bound on its amount input in the selected source.

Currency is uppercased as the user types and constrained to three characters in the form. The DTO also requires length three, and the service uppercases before storing. Unlike merchant and description, currency is not trimmed in the selected service assignment. A three-character length rule does not establish membership in an approved currency-code set. If the product needs that guarantee, it requires an explicit rule and fixtures.

Category comes from a select list in the UI and an enum property in the request. Date comes from a date input and a DateOnly property. Required attributes on value-type properties should not be casually interpreted as proof that every missing, default, or undefined enum value is rejected in every binding configuration. A precise endpoint claim needs binding tests for the actual serializer and validation pipeline.

## Model validation and business policy answer different questions

DTO validation checks shape and ranges before ordinary controller execution under the configured API behavior. PolicyEngine evaluates business flags such as future date, receipt requirement, and category warning. ExpenseService enforces ownership and editable state for Update and lifecycle rules for later Submit.

A request can be structurally valid yet business-ineligible for submission. For example, a valid positive amount and description can still require a receipt. Saving a Draft with a blocking policy flag is not necessarily the same operation as submitting it. The form's tip explicitly directs users to attach a receipt and submit after saving.

A test should therefore avoid labeling every policy flag as a failed create request. Trace the actual service method: Create assigns values, refreshes policy, stores the expense, and returns it. Submit is the method that checks HasBlocking before transition. The difference supports a workflow where users save incomplete drafts and resolve policy issues later.

## The default date is evaluated at module initialization

The today helper returns the UTC date portion of a new Date, but EMPTY calls it when the module initializes. New component instances use that stored EMPTY object as their initial state. A long-lived loaded module can therefore retain the earlier date as its default if a new form is opened after the relevant day boundary.

This is a source-derived freshness issue, not a browser incident measured in this course. A proposed test can control module import time, advance the clock, and mount a new form to observe its initial date. Another test can compare the UTC date default with a user's local calendar expectation. These are different questions: when the value is computed and which timezone defines today.

A small proposal could initialize state through a function that creates a fresh object and date at component initialization. That would address module-lifetime freshness but not automatically choose the correct business timezone. The requirement should specify whether expense dates default to local user date, organization date, or UTC date.

## Edit loading can race with navigation

The edit effect fetches by id and sets form state when the promise resolves. The selected effect does not include an active flag or abort mechanism. If the component remains mounted while id changes, responses can arrive out of order and an older request can overwrite newer form data under a controlled interleaving.

This is a testable source hypothesis. Use deferred responses for expense A and expense B: begin A, switch to B, resolve B, then resolve A. Observe which values remain. A passing repair test should require B's data to remain authoritative after the late A response. It should also ensure a legitimate A load still works when no navigation occurs.

Do not fix this by discarding every late response based on elapsed time. Request identity or cancellation tied to the current id gives a clearer contract. Also decide whether changing id should reset error and busy state, because those values belong to the displayed form context rather than the old request.

## Errors need a field-level contract

The component renders err.message and an optional details array. The API wrapper does not flatten arbitrary model-validation errors into that array. Therefore, a backend validation response may not appear as field-level messages without additional normalization. A source review can identify this mismatch, but a host-and-component test is needed to establish the exact rendered experience.

A proposed design can return stable field identifiers and messages, preserve general business errors separately, and focus the first invalid field. The acceptance criteria should distinguish local browser prevention, server model validation, business policy, and stale-state rejection. Combining every case into one red message can make recovery unclear even when the request is correctly rejected.

## Independent exercises

Exercise NR11-A: trace amount through raw input, parseFloat, JSON, DTO binding, and domain storage. Compare browser-only constraints with the server upper bound and identify which direct callers bypass which validation layers.

Exercise NR11-B: design the module-date freshness fixture and the out-of-order edit-load fixture. State separate acceptance criteria for current request identity and business-date semantics.

Exercise NR11-C: specify a draft-save versus submit workflow with one blocking receipt flag. Explain why a successful draft save does not prove submission eligibility and design an error presentation contract for all four validation layers.
