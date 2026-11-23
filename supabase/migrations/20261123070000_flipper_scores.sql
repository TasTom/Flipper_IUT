begin;
create schema if not exists flipper_private;

create table if not exists flipper_private.scores (
    id bigint generated always as identity primary key,
    run_id uuid unique not null,
    initials text not null check (initials collate "C" ~ '^[A-Z]{3}$'),
    score integer not null check (score > 0)
);
alter table flipper_private.scores enable row level security;
create index if not exists flipper_scores_rank
    on flipper_private.scores (score desc, id asc);
revoke all on schema flipper_private from public, anon, authenticated;

create or replace function public.flipper_top_scores()
returns table (initials text, score integer)
language sql stable security definer set search_path = ''
as $$
    select s.initials, s.score
    from flipper_private.scores s
    order by s.score desc, s.id
    limit 100;
$$;

create or replace function public.flipper_submit_score(p_run_id uuid, p_initials text, p_score integer)
returns boolean
language plpgsql security definer set search_path = ''
as $$
begin
    if p_run_id is null or p_initials is null or p_initials collate "C" !~ '^[A-Z]{3}$'
        or p_score is null or p_score <= 0 then
        raise exception 'Invalid flipper score' using errcode = '22023';
    end if;

    perform pg_catalog.pg_advisory_xact_lock(807383, 100);
    if exists (select 1 from flipper_private.scores s where s.run_id = p_run_id) then
        return exists (
            select 1 from flipper_private.scores s
            where s.run_id = p_run_id and s.initials = p_initials and s.score = p_score
        );
    end if;

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

revoke all on function public.flipper_top_scores() from public, anon, authenticated;
revoke all on function public.flipper_submit_score(uuid, text, integer) from public, anon, authenticated;
grant execute on function public.flipper_top_scores() to anon;
grant execute on function public.flipper_submit_score(uuid, text, integer) to anon;
commit;
