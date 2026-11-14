ps_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mov r0, v0
mov r1.x, l(0)
loop
ige r1.y, r1.x, cb0[1].x
breakc_nz r1.y
mad r0, r0, cb0[0], v0
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
