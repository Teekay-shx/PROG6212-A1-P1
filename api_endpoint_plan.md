# RaceDay System — API Endpoint Plan

This plan covers Authentication, User Profile, Events, Categories, Event Enrolments, and Results, plus
supporting Payment endpoints identified as necessary to support the Enrolment workflow.

Roles: **Public** (no login required), **Any** (any logged-in user), **Participant**, **Organiser**, **Admin**.
"Organiser (owner)" means the Organiser who owns the Event that the resource belongs to.

## 1. Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new user account as a Participant or Organiser. | Public | `{ fullName, email, password, role }` | 201 Created — new user object (no password) <br> 409 Conflict — email already registered |
| POST | /api/auth/login | Authenticates a user and issues an access token. | Public | `{ email, password }` | 200 OK — `{ token, user }` <br> 401 Unauthorized — invalid credentials |

## 2. User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the profile of the currently logged-in user. | Any | None | 200 OK — user profile object |
| PUT | /api/users/me | Updates the logged-in user's own profile details. | Any | `{ fullName, phone }` | 200 OK — updated profile <br> 400 Bad Request — invalid fields |
| GET | /api/users/{id} | Returns a specific user's details for administration. | Admin | None | 200 OK — user object <br> 404 Not Found |
| DELETE | /api/users/{id} | Deactivates/removes a user account. | Admin | None | 200 OK — confirmation <br> 404 Not Found |

## 3. Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists all published events, with optional filters (date, location). | Public | None | 200 OK — array of events |
| GET | /api/events/{id} | Returns full details of a single event. | Public | None | 200 OK — event object <br> 404 Not Found |
| POST | /api/events | Creates a new event owned by the logged-in Organiser. | Organiser | `{ eventName, eventDate, location, description }` | 201 Created — event object <br> 400 Bad Request |
| PUT | /api/events/{id} | Updates an existing event's details. | Organiser (owner) | `{ eventName, eventDate, location, description, status }` | 200 OK — updated event <br> 403 Forbidden <br> 404 Not Found |
| DELETE | /api/events/{id} | Deletes/cancels an event. | Organiser (owner) or Admin | None | 200 OK — confirmation <br> 404 Not Found |

## 4. Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists all race categories for a given event. | Public | None | 200 OK — array of categories <br> 404 Not Found (event) |
| POST | /api/events/{eventId}/categories | Adds a new category (e.g. 10km, 21km) to an event. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 201 Created — category object <br> 400 Bad Request |
| PUT | /api/categories/{id} | Updates a category's details. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 200 OK — updated category <br> 403 Forbidden <br> 404 Not Found |
| DELETE | /api/categories/{id} | Removes a category from an event. | Organiser (owner) | None | 200 OK — confirmation <br> 404 Not Found |

## 5. Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/categories/{categoryId}/enrolments | Enrols the logged-in participant into a category (subject to `maxParticipants`). | Participant | `{ }` (bibNumber assigned by system) | 201 Created — enrolment object <br> 404 Not Found — category <br> 409 Conflict — already enrolled or category full |
| GET | /api/users/me/enrolments | Lists all enrolments belonging to the logged-in participant. | Participant | None | 200 OK — array of enrolments |
| GET | /api/categories/{categoryId}/enrolments | Lists all participants enrolled in a category. | Organiser (owner) or Admin | None | 200 OK — array of enrolments <br> 404 Not Found |
| PUT | /api/enrolments/{id}/status | Updates an enrolment's status (e.g. Confirmed, Cancelled). | Organiser (owner) or Admin | `{ status }` | 200 OK — updated enrolment <br> 404 Not Found |
| DELETE | /api/enrolments/{id} | Cancels the participant's own enrolment. | Participant (own) or Admin | None | 200 OK — confirmation <br> 404 Not Found |

## 6. Payments (supporting Enrolments)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{id}/payment | Records payment of the entry fee for an enrolment. | Participant (own) | `{ amount, paymentMethod }` | 201 Created — payment object <br> 404 Not Found — enrolment |
| GET | /api/enrolments/{id}/payment | Retrieves payment details for an enrolment. | Participant (own) or Admin | None | 200 OK — payment object <br> 404 Not Found |

## 7. Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{id}/result | Captures the finish time and ranking for a participant after the race. | Organiser (owner) or Admin | `{ finishTime, overallRank, categoryRank, status }` | 201 Created — result object <br> 404 Not Found — enrolment |
| PUT | /api/results/{id} | Corrects/updates a previously captured result. | Organiser (owner) or Admin | `{ finishTime, overallRank, categoryRank, status }` | 200 OK — updated result <br> 404 Not Found |
| GET | /api/events/{eventId}/results | Returns the published results/leaderboard for an entire event. | Public | None | 200 OK — array of results |
| GET | /api/enrolments/{id}/result | Returns the result for one specific enrolment. | Any (own) or Admin | None | 200 OK — result object <br> 404 Not Found |
