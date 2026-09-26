ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB1[1], immediateIndexed
dcl_resource_buffer (mixed,mixed,mixed,mixed) t0
dcl_resource_buffer (uint,uint,uint,uint) t1
dcl_output o0
dcl_temps 1
ld_indexable(buffer)(uint,uint,uint,uint) r0.x, cb1[0].x, t1.x
and r0.x, r0.x, l(3)
ld_indexable(buffer)(mixed,mixed,mixed,mixed) o0, r0.x, t0
ret
