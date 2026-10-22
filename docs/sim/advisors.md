# Advisors

Each player keeps a list of advisors. Four have a field role (0 research,
1 espionage, 2 construction, 3 shipbuilding) and work for the whole
empire; the rest are governors (role 5), one per colonized star. An
advisor has four skills, one per field.

## Value

An advisor's value for a field is its skill for that field when it is a
governor or holds that field's role, else 0. An advisor turned by an
enemy spy gives the negated value, so it works against its owner. When a
player is eliminated, every advisor it had turned is loyal again
.

## Growth (every 2000 ticks)

The advisors task visits every player, eliminated ones too,
and each governor in its list. Field advisors never grow.

1. k = R(4) picks a skill; n = that skill in hundredths, as a whole number.
2. r = R(n). If r < 0.3 (so r is 0) nothing happens; the chance to grow is
   (n - 1) / n.
3. The skill grows by 0.01, not past 0.25. Politician species grow by 0.02,
   not past 0.3.

Every governor draws twice, even one at its cap.

## Orders

- Fire a governor (action 0): it is removed and its star gets
  a new governor.
- Appoint a governor to a field (action 1): the field's current advisor is
  removed, the governor takes the role with every other skill set to 0 and
  no star, and its star gets a new governor. It costs nothing.

New advisors go to the front of the list.

## Computer players

Run second in a computer player's turn (`ai/computer-players.md`).

1. Each governor whose four values sum below 0 (a turned one) is replaced
   by a new governor at the same star, restarting the scan after each.
2. For each field 0 to 3: the governor with the best value for it (the
   later one in the list on ties) is appointed when the field has no
   advisor or the governor's value is strictly higher than the advisor's.

Computer players never hire or fire otherwise.
