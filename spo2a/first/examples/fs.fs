let main () =
    let numbers = [25; -1233; 10; 12310; -1512; 7123; -203; 1233; 12321; 12312]
    let mutable sum = 0
    let mutable count = 0

    for i in 0 .. numbers.Length - 1 do
        let num = numbers.[i]

        if num > 0 then
            if num % 2 = 0 then
                sum <- sum + num
                count <- count + 1
            elif num % 3 = 0 then
                sum <- sum + num * 2
                count <- count + 1
            else
                sum <- sum + num / 2
        elif num < 0 then
            match num with
            | n when n < -300 ->
                sum <- sum - 10
            | n when n < -200 ->
                sum <- sum - 5
            | n when n < -100 ->
                if abs n > 75 then
                    sum <- sum - 2
            | _ ->
                sum <- sum + num
        else
            count <- count + 1

    let mutable whileIndex = 0
    while whileIndex < 2 do
        sum <- sum + whileIndex
        whileIndex <- whileIndex + 1

    let mutable doIndex = 0
    let mutable continueDo = true
    while continueDo do
        count <- count + doIndex
        doIndex <- doIndex + 1
        if doIndex >= 2 then
            continueDo <- false

    printfn "Sum: %d, Count: %d" sum count

    let x = 1
    let y = 0

    let mutable isValid = false

    if x > 0 then
        if y > 0 then
            isValid <- true
        elif y = 0 then
            isValid <- false
        else
            isValid <- false
    elif x = 0 then
        isValid <- y > 0
    else
        isValid <- false

    printfn "Valid: %b" isValid

main ()