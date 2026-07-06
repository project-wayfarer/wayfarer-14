## Corporation cartridge program name
corp-program-name = Corporation Manager

## Header label
corp-section-invites = Pending Invitations
corp-section-members = Members
corp-section-public = Corporations

## Member table columns
corp-col-rank = Rank
corp-col-name = Name
corp-col-actions = Actions

## Status labels
corp-not-in-corp = You are not in a corporation.
corp-no-description = No description set.
corp-no-public-corps = No corporations found.
corp-member-self-suffix =  (you)
corp-member-count = {$count} member(s)

## Privacy labels
corp-privacy-public = Public
corp-privacy-unlisted = Unlisted
corp-privacy-private = Private

## Bank
corp-bank-balance = Corp Bank: ${ $balance } spesos

## Rank names
corp-rank-member = Member
corp-rank-recruiter = Recruiter
corp-rank-manager = Manager
corp-rank-leader = Leader

## Invite row
corp-invite-row = {$name} ({$members} member(s))

## Create view
corp-create-title = Found a Corporation
corp-create-name = Name:
corp-create-name-placeholder = Corporation name…
corp-create-name-permanent = ⚠ The corporation name cannot be changed after creation.
corp-create-description = Description:
corp-description-limit = {$current}/{$max} characters
corp-create-privacy = Privacy:
corp-create-cost = ⚠ Founding a corporation costs $1,000,000 spesos.

## Edit description view
corp-editdesc-title = Edit Description
corp-editdesc-privacy = Privacy:

## Buttons
corp-btn-create = Create New Corporation
corp-btn-found = Found Corporation
corp-btn-cancel = Cancel
corp-btn-save = Save
corp-btn-join = Join
corp-btn-request-invite = Request Invite
corp-btn-leave = Leave Corporation
corp-btn-leave-confirm = Are you sure?
corp-btn-disband = Disband Corporation
corp-btn-disband-confirm = Are you sure?
corp-btn-invite = Invite Member
corp-btn-send-invite = Send Invitation
corp-btn-edit-desc = Edit
corp-btn-toggle-privacy = Change Privacy
corp-btn-cycle-privacy = Cycle Privacy
corp-btn-promote = Promote
corp-btn-demote = Demote
corp-btn-kick = Kick
corp-btn-kick-confirm = Are you sure?
corp-btn-accept = Accept
corp-btn-decline = Decline

## Invite view
corp-invite-title = Send Invitation
corp-invite-character = Character:
corp-invite-warning = Please send invites only to people who are expecting one. Spamming invites may result in corporation termination without refund.

## Error messages (returned as loc keys from server)
corp-error-already-member = You are already a member of a corporation.
corp-error-invalid-name = Corporation name must be between 1 and 40 characters.
corp-error-name-taken = A corporation with that name already exists.
corp-error-insufficient-funds = Insufficient funds. You need $1,000,000 spesos.
corp-error-not-found = Corporation not found.
corp-error-invite-required = This corporation requires an invite.
corp-error-not-in-corp = You are not in a corporation.
corp-error-leader-cannot-leave = The leader cannot leave while other members remain. Disband or transfer leadership first.
corp-error-no-permission = You do not have permission to do that.
corp-error-member-not-found = Member not found.
corp-error-player-not-found = That character could not be found on the station.
corp-error-target-in-corp = That player is already in a corporation.
corp-error-already-invited = That player has already been invited.
corp-error-invite-not-found = Invite not found or already expired.
corp-error-invalid-rank = Invalid rank.

## Corporation station
corp-section-station = Your Corporation Station
corp-section-station-unavailable = Currently station platforms are unavailable.
corp-station-label = Station:
corp-station-none = No station purchased.
corp-create-station-name = Station Name:
corp-create-station-placeholder = Enter station name…
corp-btn-purchase-station = Purchase Station ($5,000,000)
corp-station-scanner-visible = Show on Shuttle Scanners
corp-station-coords = Coordinates: {$x}, {$y}
corp-station-upkeep-cost = Upkeep: {$amount} spesos / round
corp-station-upkeep-warning = Insufficient funds to cover next upkeep!
corp-banner-upkeep-warning = Warning: Your corporation cannot afford the station upkeep.
corp-notify-upkeep-charged = [CORP] Station upkeep of {$amount} spesos has been deducted from your corporation's bank account.
corp-notify-upkeep-evicted = [CORP] Your corporation could not afford the station upkeep of {$amount} spesos. The station has been blasted from the sector by Zekke landlords and all materials has been lost.
corp-notify-low-balance-warning = [CORP] Warning: {$corpName}'s account balance ({$balance} spesos) is less than this shift's station upkeep ({$upkeep} spesos). Deposit funds into the corporation ATM before shift end or the station will be evicted.
corp-notify-invited = [CORP] You have been invited to join {$corp}. Check your PDA's Corporation app to accept or decline.
corp-error-station-exists = Your corporation already has a station.
corp-error-station-name-empty = Please enter a station name.
corp-error-station-name-too-long = Station name must be 40 characters or less.
corp-error-station-purchase-disabled = Corporation station purchasing is currently disabled.
