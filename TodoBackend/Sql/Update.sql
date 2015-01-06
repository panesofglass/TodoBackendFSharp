update Todo
set Title = @title
  , Completed = @completed
  , [Order] = @order
where Id = @id
