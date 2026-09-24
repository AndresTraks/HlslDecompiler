ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_output o0
dcl_temps 4
mov r0, cb0[0]
mov r1, cb0[0].wzyx
mov r2.x, l(0)
loop
ige r2.y, r2.x, cb0[1].x
breakc_nz r2.y
add r3, r0, l(0.25, 0.25, 0.25, 0.25)
mul r0, r1, l(0.5, 0.5, 0.5, 0.5)
iadd r2.x, r2.x, l(1)
mov r1, r3
endloop
mad o0, r1, l(2, 2, 2, 2), r0
ret
