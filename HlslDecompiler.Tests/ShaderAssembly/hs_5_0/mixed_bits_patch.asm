hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0]
dcl_input v[3][1].x
dcl_output o0
dcl_output o1.x
dcl_temps 1
mov r0.x, vOutputControlPointID
mov o0, v[r0.x][0]
lt r0.y, l(0), v[r0.x][0].w
iadd r0.z, vOutputControlPointID, v[r0.x][1].x
movc o1.x, r0.y, v[r0.x][0].z, r0.z
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_input vicp[3][1].x
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
mov r0.x, vForkInstanceID.x
mov o[r0.x].x, vicp[0][1].x
ret
hs_fork_phase
dcl_input vicp[3][1].x
dcl_output_siv o3.x, finalTriInsideTessFactor
utof o3.x, vicp[1][1].x
ret
hs_fork_phase
dcl_input vPrim
dcl_input vicp[3][0].xy
dcl_output o0.y
dcl_temps 1
lt r0.x, l(0), vicp[2][0].x
movc o0.y, r0.x, vicp[2][0].y, vPrim
ret
