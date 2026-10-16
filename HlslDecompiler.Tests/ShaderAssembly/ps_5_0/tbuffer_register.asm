ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_buffer (mixed,mixed,mixed,mixed) t2
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
and r0.x, v1.x, l(3)
iadd r0.x, r0.x, l(1)
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r0, r0.x, t2
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r1, l(0, 0, 0, 0), t2
mad o0, r1, v0, r0
ret
