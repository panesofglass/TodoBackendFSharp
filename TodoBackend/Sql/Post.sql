insert into Todo (Title, Completed, [Order])
values (@title, @completed, @order)
select IDENT_CURRENT ('Todo')