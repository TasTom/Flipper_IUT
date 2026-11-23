begin;

create table flipper_private.run_tickets (
    id uuid primary key default pg_catalog.gen_random_uuid(),
    started_at timestamptz not null default pg_catalog.clock_timestamp()
);
alter table flipper_private.run_tickets enable row level security;
create index flipper_run_tickets_started on flipper_private.run_tickets (started_at);

create function public.flipper_begin_run()
returns uuid
language plpgsql security definer set search_path = ''
as $$
declare
    ticket uuid;
begin
    delete from flipper_private.run_tickets
    where started_at < pg_catalog.clock_timestamp() - interval '6 hours';
    insert into flipper_private.run_tickets default values returning id into ticket;
    return ticket;
end;
$$;

create or replace function public.flipper_submit_score(p_run_id uuid, p_initials text, p_score integer)
returns boolean
language plpgsql security definer set search_path = ''
as $$
declare
    started timestamptz;
    elapsed_seconds double precision;
begin
    if p_run_id is null or p_initials is null or p_initials collate "C" !~ '^[A-Z]{3}$'
        or p_score is null or p_score <= 0 then
        raise exception 'Invalid flipper score' using errcode = '22023';
    end if;

    select started_at into started from flipper_private.run_tickets
    where id = p_run_id for update;
    if not found then
        raise exception 'Missing or expired flipper ticket' using errcode = '22023';
    end if;

    if exists (select 1 from flipper_private.scores s where s.run_id = p_run_id) then
        return exists (
            select 1 from flipper_private.scores s
            where s.run_id = p_run_id and s.initials = p_initials and s.score = p_score
        );
    end if;

    elapsed_seconds := extract(epoch from pg_catalog.clock_timestamp() - started);
    if elapsed_seconds < 5 or elapsed_seconds > 21600 or p_score > 2000000 + elapsed_seconds * 200000 then
        raise exception 'Flipper score outside the permitted range' using errcode = '22023';
    end if;

    perform pg_catalog.pg_advisory_xact_lock(807383, 100);
    if exists (
        select 1 from flipper_private.scores s
        where s.score >= p_score offset 99 limit 1
    ) then
        return false;
    end if;

    insert into flipper_private.scores (run_id, initials, score)
    values (p_run_id, p_initials, p_score);
    return true;
end;
$$;

revoke all on function public.flipper_begin_run() from public, anon, authenticated;
grant execute on function public.flipper_begin_run() to anon;
commit;
